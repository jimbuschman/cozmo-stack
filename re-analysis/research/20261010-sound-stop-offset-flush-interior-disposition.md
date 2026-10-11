# Q14-119: music flush interior-entry navigation

Checkpoint775; scope guard reread. Same engine hash. Primary `20261010-sound-stop-offset-flush-interior-native.txt`, `-interior-boundaries-native.txt`; reproducible external-to-body navigation `-interior-callers.py/.json`. The exact source ranges areA71634..A716BC (context-specific flush) andA716BC..A717A4 (raw-mode flush). Known MUC9/MUC10 and SR756/SF757 contracts remain paid; this checks only their actual input frontier.

53 encoded external-to-body candidates:3 ARM and50 Thumb. Primary rejects all THREE apparent ARM sites:5F0B7C is inside actual four-byte ThumbMVN at5F0B7A followed bySUBS5F0B7E;8A7280 and8A735C are actual ThumbCMP followed byBLT, not ARM BLLE. They do not provide manager/context/mode inputs into these ARM music functions. All50 Thumb encodings lie in the ARM music address region; no below95E540 Thumb call site is supplied. Do not claim all50 exact code boundaries or arbitrary computed paths are proved absent from this location fact alone.

ActualA716B8 returns throughPOP PC;A716BC begins a separate PUSH. At the second body's end,A71798 returns;A7179C is an internal branch arm back toA71770, before separateA717A4 PUSH. PredecessorA7162C unconditionally branches backward toA715C0, followed by literalA71630; it does not fall through toA71634. These source boundaries do not establish an external/interior caller.

The original manager/context admission toA71634 and manager/raw-mode admission toA716BC remain UNKNOWN. No positive supplied interior input is established by this bounded pass. Stop this lead without re-extracting note release, queue/removal or storage descendants. Original119 retains writer/alias and those named admission gaps, PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: retained-event/list backing and storage release implementations; exact stop, unlink and callback decisions remain the existing paid contracts.
