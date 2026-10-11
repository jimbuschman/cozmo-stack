# Q14-119: bounded stop-offset store attribution

Checkpoint614; decisions only, guard reread before each step. Companion `20261010-sound-stop-offset-writer-frontier-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

Specific ARM immediate-store navigation to offset1F8 found nine encodings in ELF.text, retained in `20261010-sound-stop-offset-store-candidates.jsonl`. Six positive offsets are actual PBI paths:988264 derived min setter,9886D0 derived one-shot clear,9CBAD8 ordinary one-shot clear,A00318 base ctor sentinel,A3E958 timed release eventtime/sentinel,A3EB88 forced-stop sentinel. All six already have primary contracts SO541/SJ542/SF543/BI425/HN518; fresh native confirms their exact live producers rather than new bodies.

The three negative-offset matchesA90278/A902DC/A90338 are positively stack-local stores: respective bases derive from SP390 plus indices atA90260/64,A902B8/C0,A90324/2C. They do not name PBI+1F8. No descent into their surrounding calculations or collaborators.

HN518's actual timed release stores eventtime toPBI38 or9E805C attachment before context10 query. Query nonzero marksentry3 then usesFFFFFFFF; queryzero/entry3 stores raw event18 and chooses arg2 by exactFFFFFFFF equality. Forced-stop A3EB4C storesFFFFFFFF beforeA01280 with arg2=1. Derived one-shot getter always clears1F8 before suppressionbit1 result; ordinary getter likewise always clears and returns oldword. Derived setter uses actual live164 scalar, native F32 conversion/division/rounding/min gates already SO541C and SF543 sender/recipient. A05574 composite position identity and duration A56478->A0393C callback/payload/order already SJ542A/SO541D/E/CB1/2/NC607; no new unread clock, duration or emit body.

PARTIAL: original explicitly exhaustive writer/callback proof still requires relative/indirect/aliased writes not covered by this immediate STR scan and exact production input binding for any such positive candidate. The actual six named writer/consumer contracts are paid; a scan is navigation, not global absence evidence. Preserve original broader music-operation input obligation rather than expanding this into a new family census.

out of scope: stack-local unrelated calculations, backing/allocator/copy infrastructure and per-sample DSP. Counts unchanged97 native CHECKED+8 scope CHECKED/35 PARTIAL/140;140 AUDIBLE/0 INTERNAL. Research only; no production/fidelity/inventory/Q15 changes.
