# Q14-104/131: output-query candidate source words

Checkpoint800; scope guard reread before each step. Engine SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Continues788's named9FBB30 output-query input, not a new writer family. Coverage:116 CHECKED /24 PARTIAL;140 AUDIBLE /0 INTERNAL.

The138 remaining Thumb-looking encodings are each at offset2 inside a naturally aligned four-byte word. `20261010-sound-routing-query-candidate-word-witness.py/.json` records every address, exact bytes and ARM interpretation; `-candidate-word-native.txt` records each containing word and one neighboring word on either side. All138 containing words decode in ARM mode. This is an individual byte/interpretation witness, not proof that every source can only execute in ARM mode or that arbitrary alternate/computed inputs cannot exist.

Primary entry slices in `-candidate-owner-entries-native.txt` and `-candidate-entries-native.txt` expose why nearest Ghidra function labels/sizes must not be treated as enclosure:9FB734 returns at9FB784, followed by separate leaf9FB788;9FB804 returns at9FB860, followed by separate9FB864 with its own conditional frame;9FBF80 returns at9FBFC8/9FBFD8, followed by separate entry9FBFDC with LDR/PUSH. Similarly9FB4E8's out-of-line cases precede literal island9FB6C8, so navigation size480 alone does not account for all neighboring candidates. No candidate becomes a positive9FBB30 caller on these facts.

The actual query retains incoming SECOND register argumentR1 inR6 and requests zero-fill(destination,0,3C), including34/38, before its property/inheritance gates. The output destination remains UNKNOWN. The original all-writer/alias obligation is still open:bind actual admitted output destination and prove whether it can alias the node's parent34/outputbus38; also settle the remaining named destinations and non-immediate aliases in104/131. No stronger status is claimed from raw-word absence, natural alignment, nearest function labels, or this finite candidate set.

out of scope: neighboring 3D positioning payload bodies, backing allocation/copy/free implementations and DSP arithmetic; candidate word slices are navigation only and no callee was followed.

Research only; no production, manifest, inventory or acceptance changes. Q14-104/131 remain PARTIAL. Q15 remains parked behind Q14.
