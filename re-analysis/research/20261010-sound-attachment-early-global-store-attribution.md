# Q14-104/131: early fixed-global and independent member destinations

Checkpoint717; scope guard reread before each step. Primary ELF SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Evidence: music-early-fields-native.txt, early-global-inputs-native.txt and early-global-publishing-native.txt (same date/prefix).

| Original candidate | Positive destination and remaining limit |
| --- | --- |
| 9800BC / 9800C4 | Actual entry980058 loads0070D768 from980430 and ADD at9800AC resolves9800AC+8+70D768=108D81C. These clear fixed global words108D850/108D854, alongside2C/3C/44/48 and packed40 defaults; they are not stores through a runtime parent-node pointer. The branch9800A0 and publication order remain exact. This does not assign an unproved class or close global timing aliases. |
| 980758 / 980760 | Actual98056C saves incomingr2 inr4, then980698 makesFP=r4: FP is an input descriptor, not a stack-frame base. At9807C0..98080C, incomingr0 savedSP8 owns array84/count88/cap8C of4C-byte records; count advances before computed-slot-null test, and9807EC setsSL to that slot. Descriptor30 is converted by signed multiply/divide1000 into record34; descriptor34 copies to record38; descriptor3C low3 bits enter record40 before further flag decisions. These are separate embedded-record stores on this concrete path. Admitted outer receiver/descriptor and alternate entry inputs remain UNKNOWN; the conversion and parameter order are decisions, not per-sample DSP. |

No storage allocator descent; no missing caller or serialized default is an unreachable-input proof. Original parent34/output-bus38 alias obligations stay PARTIAL. Counts remain107 native CHECKED+8 scope CHECKED,25 PARTIAL;140 AUDIBLE/0 INTERNAL. Research only; no implementation or fidelity classification change.
