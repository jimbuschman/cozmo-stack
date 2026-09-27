# I-M6 gap pass 2: adjacent globals and Vorbis lifecycle

GOT slot `0x01040230` points to adjacent BSS `0x0108E638`; slots
`0x01040234..0x01040264` point to the trig views; `0x01040268` points to the
work-pointer word; `0x0104026C` and `0x01040270` point to the built-in codebook
and floor tables. The adjacency establishes linkage grouping, not aliasing.

The setup path `0x00AB6380..0x00AB6780` allocates and records setup structures
inside the decoder object. The packet inverse path `0x00AB6B14..0x00AB6F20`
gets each planar channel pointer from decoder fields and calls
`mdct_backward(n, channel)`. Neither path stores BSS `0x0108E648`, passes a
third work-buffer argument, nor copies a decoder-object pointer into it.

No evidence makes `0x0108E638` an owner or alias of `0x0108E648`; treating the
adjacent cache object as the writer would be a guess.

Classification: `RECOVERABLE_GAP`. Next pass: distinguish required observable
behavior from unrecovered allocation ownership and identify the exact next
source operation.
