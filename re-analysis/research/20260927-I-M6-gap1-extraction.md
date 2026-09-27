# I-M6 gap pass 1: direct references to the IMDCT work pointer

Question: who writes BSS word `0x0108E648`, read by `mdct_backward` through
GOT slot `0x01040268`?

The ELF relocation at `0x01040268` is `R_ARM_RELATIVE` with addend
`0x0108E648`. A complete ARM `.text` scan for PC-relative literal value
`0xFFFFFFDC` (the GOT-base displacement from `0x0104028C` to the slot) found
only `0x00AB4E50` in the Vorbis region. The other same-valued literal at
`0x004DF270` is added directly to PC and does not use the Wwise GOT base.

At `0x00AB4E34..0x00AB4E64`, the entry resolves the slot, loads the address of
the BSS word, loads its pointer value, and returns if it is zero. Later loads
at `0x00AB4FAC`, `0x00AB526C`, and `0x00AB5A2C` reuse that pointer. There is no
store through the slot in this function.

Classification: `RECOVERABLE_GAP`. Direct static GOT references do not reveal
the writer. Next pass: inspect the adjacent BSS object and Vorbis lifecycle.
