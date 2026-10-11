# Q14-104/131: modulator deadlines, fields and evaluator slots

Checkpoint688; guard reread before each step. Primary `20261010-sound-attachment-e-frontier-stores-native.txt`, `20261010-sound-attachment-state-extra-inputs-native.txt`, `20261010-sound-attachment-state-receiver-inputs-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Reuses existing MF158/MJ285/MP4 and shared MU/SV exact rows; no new Q15 recovery or edits. Ten original candidates.

9E2170/9E21A4 are actual9E2144 constructor-defined base state, table103B248 (9E2150+8+6590E8+8). It writes deadline38=7FFFFFFF and34=0. MF158 positively supplies independently allocated AC/8C state products to this constructor before derived103B268/103B288 publication, not ordinary bank nodes.

9E26B0/9E27BC are actual initializer9E266C's savedincoming state r4. The former copies request14 raw to state34; the latter copies request8 to deadline38 **after** modulator18 and state10 callbacks, **before** state14 callback. MF158/MJ285 supplies that concrete created state. No parent/outputbus semantics or callback-success normalization.

9E1F30/9E1F40 are shared SV12 envelope setter: state38 gets wrapping(state84+state78+converted rate-scaled duration), or7FFFFFFF for ordered negative input. 9E2010 is SV4's raw34=0 when state74F32 compares NE to zero, including unordered. 9E21EC is OA6/MU6's sentinel-only deadline update, state38=incoming+state3C, with state44=2 unless already3. These are exact timing/control-state writes already positively bound to MF158 products. Alternative caller destinations are not proven absent; matching class fields alone does not close original alias proof.

9E2E20/9E2E28 are MP4 actual LFO work slot: work blockbase4, count8, capacityC; selected spare addressbase+4C*oldcount, with count increment before nonnull slot initialization. Clear slot34/38 among its six oscillator words. Not original owner node34/38. Existing MP4 retains unconditional later writes for null computed destination and MP5 control-stream evolution; no graceful skip or per-sample DSP equivalence invented.

Original104/131 remain PARTIAL for admitted destinations and alias questions. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
