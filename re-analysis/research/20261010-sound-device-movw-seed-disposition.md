# Q14 rate/frame: omitted ARM device-address seed disposition

Checkpoint778; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. `20261010-sound-device-movw-seed-expansion.py/.txt` adds the low-half DF90..DF9B MOVW seed to checkpoint774's exact-known-compare local navigation. The previous ARM MOVW seed included the105243C..4F rate words but omitted108DF90..9B device words. No old primary contract is discarded by fixing this omission.

The run processes4393 windows, one more than774's4392. It retains exactly the same300 events, no added or missing event, and326 capped windows. The added aligned wordE06F80=030DFF96 encodes a putative MOVW low-halfDF96. Raw bytes are preserved in `-seed-native.txt`; ELF section metadata in `-seed-sections.json` places it in non-executable .rodata BE3ED0..1017518. It establishes no code writer or actual input. LOAD-wide mixed-mode navigation includes data, so section attribution is retained explicitly.

Known direct scalar writers and RC654 helper ABIs remain paid. This narrow correction is not a complete target-generation or alias proof: Thumb MOVW entry seeds, shifted addresses, stack/copy/foreign aliases, unknown compare/loop bounds and capped windows still require their own positive source checks. The original rate/frame obligations043/092/099/120/124/130 remain PARTIAL for their independently named alias, zero/provider and startup-input questions. No default/serialized-zero/caller-absence unreachability claim.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: storage/I-O infrastructure and per-sample DSP arithmetic; no descent from this data encoding.
