# Q14-021 fixed engine symbol-name inputs, checkpoint844

Scope guard reread before each step. A selected symbol name can identify a callable optional-layer input, so that caller-side selection is checked. out of scope: loader implementation, file/open paths, symbol hash/resolution infrastructure and backing. No loader body descent.

Primary `20261011-sound-optional-layer-symbol-lookup-native.txt` and literal/string/relocation witnesses in `-callers.py/.json`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Navigation covers exact immediate executable-section ARM/Thumb branches to this engine's actual dlsym PLT4D6244; its two candidate calls are real ARM instructions. Aligned raw PLT-pointer navigation yields no extra match; that is not an indirect-call absence proof.

| Positive selection | Primary contract and boundary |
| --- | --- |
| Actual import interface | PLT4D6244+8+B00000+7A000+BAC=1050DF8; actual ELF ARM_JUMP_SLOT at1050DF8 names dlsym. No initial resolver word is treated as a completed runtime binding. |
| 953174 | Actual entry95314C tests initialized byte, returns if nonzero. On first path, literalword953198=2F2561 and PC95316C+8 form C456D5, whose exact terminated bytes are `__android_log_buf_write`. R0=FFFFFFFF before imported lookup. Returned pointer is stored before initialized byte1. No caller-controlled optional-layer symbol name is supplied by this call. Stop before logging/crash/loader recipients. |
| A5733C | Nonzero handle gateA5732C..30 precedes name selection. LiteralwordA575D4=550848 and PCA57338+8 form FA7B88, exact terminated bytes `g_pAKPluginList`. Missing symbol selects raw7; nonnull loads returned symbol's first word and invokes9CBAE0 atA57350. PR1–4/841 own linked registration and conditional callback20=context-provider,1,cookie24. Do not invent an optional-layer call from the existence of a dynamically supplied registration list. |

Neither positively selected name is one of the five OL649 optional-layer exports. This pays these exact engine lookup-name selections; it does not prove no alternate/computed/relative dlsym use, no companion-library dynamic lookup, no message/reflection input, or no registration callback that can reach another entry. OL649's27 companion imports and OL661's78 managed assemblies/91 declared imports retain only their stated finite evidence. No new plug-in family or optional creator/generator descent is introduced.

Q14-021 remains PARTIAL: positively establish admitted manager/tag/control and populated matching layer into RemovePersistentLayer58EE4C, or another original optional-layer producer, or provide a complete bounded supported-input exclusion proof. OL664's conditional RemovePersistentLayer→AddLayer remains paid; no fade duration, zeroed sample, or safe nonpositive control is invented. Stop these two fixed-name lookup leads.

117 CHECKED (109 native+8scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, PROJECT_STATE, hardware or Q15 edits.
