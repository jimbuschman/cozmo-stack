# I-M6 gap pass 3: work-buffer ownership boundary

The complete recovered call contract is two arguments:
`mdct_backward(n, in)` at `0x00AB6F04`. Helpers mutate `in`; the recursive
network and final rotation read from the pointer held in BSS `0x0108E648`; the
tail writes the final values to `in`. A null global makes the entry return
without changing `in` (`0x00AB4E5C..0x00AB4E64`).

Three static passes did not find the writer or lifetime of the BSS pointer.
The behavior needed by the C# decoder is nevertheless bounded: allocate a
work area large enough for the recovered indices, preserve the exact phase
ordering, and keep it decoder-internal. That is an explicit implementation
buffer, not a claim that the native ownership path was recovered.

Classification: the native writer/lifetime remains `RECOVERABLE_GAP` after
the permitted three passes. To settle it, trace stores to runtime address
`0x0108E648` during Vorbis initialization (watchpoint), then reopen the writing
function and its allocation/free call sites in the shipped `.so`. Until that
is done the manifest must say exactly this; it must not call the native
allocation/alias path source-exact.
