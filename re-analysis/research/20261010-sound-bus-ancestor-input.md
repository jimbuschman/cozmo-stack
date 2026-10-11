# Q14-104 / Q14-131: raw shipped bus and ancestor inputs

Checkpoint620; decisions only; scope guard reread before each step. Explicit original bus/ancestor/attachment census, not a new family request. `20261010-sound-bus-ancestor-input-check.py` independently reads the actual six-bank ZIP/HIRC bytes; `20261010-sound-bus-ancestor-input-witness.json` retains every bank/payload hash, ordinal, byte offsets, raw common-header bytes, bus parent and per-node ancestor walk. No exported JSON or C# parser supplies these values. Primary companion `20261010-sound-bus-ancestor-input-native.txt`, same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

## Finite supplied inputs

All15 buses are inInit.bnk. Bus-parent field is rawpayload+4 (NB16); two authoredroots3803692087 and805203703 haveparent0. The other13 resolve into3803692087, with maximum three buses in an ancestor path. Every parent exists, with no cycle in these raw inputs. Root publication/order/failure remains NB16/ROOT, not an assumed topological or numeric sort.

All3490 audio-node common-header prefixes are independently consumed according to the known source/music prefix before nativeNB2.3475 have nonzero ordinaryparent IDs,15 haveparent0;24 have explicit nonzero output-bus IDs. Every ordinary parent exists in this finite node set and every explicit output bus exists in the15-bus set. Per-node direct first-bus walk (current explicit bus, otherwise ordinary parent) reaches a bus for all3490 without a cycle. This is an authored **input graph**, not a proof that every object is played or that runtime pointers are immutable.

| First explicit bus in authored walk | Node prefixes |
| --- | --- |
| 393239870 | 104 |
| 792781730 | 337 |
| 1551306167 | 15 |
| 1723505802 | 2721 |
| 2459405053 | 9 |
| 2476517424 | 37 |
| 3803692087 | 141 |
| 3829101743 | 126 |

## Actual native consumers and retained boundary

NB2 reads first-parent/output-bus ID then second ordinaryparent ID, resolving registryselectors1 then0. Missing first nonzero ID returns2; missing second continues. Retain child before parent28 attachment, release parent before raw-result test. NB16's bus parent isselector1; missing parent2, then childretain/parent28/parentrelease/raw-result order. Parent0 publishes first root or second root under the exact existing-slot gates; no generic parent assignment is substituted.

SL288F fresh9F4BB8..4BD8 checks currentnode38 first; nonnull returns it. Otherwise reloadnode34; nullreturns0, nonnull repeats. No key comparison, IDlookup, virtual factory, retain, cycle guard or collapsed-bus eligibility check inside this resolver. Input-key construction by its callers does not add arguments to its decision body. Ordinary child setters9F1C40 and9F1EE8 and the distinct bus20 wrapper9C581C are already positively bound byOR545/RC546; CF15-18,ROOT/LF own exact validation/count/publication/retain/release. D/V/NPA/RG/RC/Y1 retain effective-parameter composition and cache gates; no per-sample descent.

Current104/131 remain PARTIAL solely for their **explicit complete runtime34/38 attachment writer/alias and admitted input/caller census**, including callback-mediated changes to the9F1EE8 local pointer and concrete additional class setter products identified by a positive writer. Authored census and named9F4BB8 decision body are paid. Do not retain a generic unread resolver claim, infer runtime absence from this raw graph, or declare arbitrary externally supplied cycles impossible. Complete caller/writer proof must use concrete positive attribution or bounded unreachability evidence.

out of scope: graph/hash/backing/allocator/I-O and positioning payloads; per-sample DSP. Counts unchanged100 native+8 scope CHECKED/32 PARTIAL/140;140 AUDIBLE/0 INTERNAL. Research only; no production/fidelity/inventory/PROJECT_STATE/hardware/Q15 edits. Next bounded step: actual ordinary/bus pointer setters and input/alias publication census.
