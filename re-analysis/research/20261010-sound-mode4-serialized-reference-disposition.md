# Q14-009/034: every serialized reference to the two mode4 nodes

Checkpoint789; scope guard reread. `20261010-sound-mode4-serialized-reference-navigation.py/.json` independently rereads all six shipped banks from AudioAssets.zip, validates complete HIRC extents and all5350 payload hashes against PB647's earlier witnesses, and searches every byte alignment for exactly725225627 and196431345. This asks whether a non-child serialized field supplies the still-open input, rather than repeating the ordinary Play graph. No excluded branch payload is decoded or traced.

There are13 matches, all in English(US)/Cozmo.bnk. `20261010-sound-mode4-serialized-field-witness.py/.json` attributes every match with native TR6/8, CF3/NB2 and TR16 geometry, saved again in `-field-native.txt` (engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1). Every field/value/offset and enclosing payload hash remains in the JSON.

| Count | Exact serialized role | Native/bank join |
| --- | --- | --- |
|2| Own object ID words, one for each mode4 node | HIRC envelope and payload word0; not a caller |
|2| ActorMixer62050212 child725225627; outer725225627 child196431345 | PB647/RJ423 verified child-vector count/words |
|8| Six Sound headers second-parentID196431345; inner196431345 and sibling990835622 second-parentID725225627 | TR6/8 Source header14 places Sound common prefix at payload18; RanSeq starts at4. CF3's inheritance/count bytes are0/0, so consume2. NB2 then consumes flag0, first parentID0, then the second parent word at relative25(Sound) or11(RanSeq), using selector0 if the applicable common reader runs. Missing parent still follows NB2's original conditional continuation; no successful lookup is invented. |
|1| Outer725225627 playlist second entry ID196431345 | Native TR16 U16 count2 and physical8-byte pairs. Independently checked pairs are990835622/50000 then196431345/50000, at playlist offset6106938. The second ID occurs at payload78, separate from the child-vector reference at60. |

The six Sounds are64408167,462381941,593468796,700326323,799826471 and983052347. Their prefix/first-parent zeros are raw positive inputs, not global immutable defaults. Child-vector order and playlist order remain distinct. None of the13 matches is an Action target, MusicSwitch leaf/transition target, or a further raw serialized supplier of these exact IDs in the5350 HIRC payloads. This settles that finite byte-reference question only. ActorMixer Play remains the already recovered no-op; parent attachment/inheritance words do not substitute for an admitted Play request into the outer graph.

PB647 ordinary723 Play-root expansion and MT651 authored MIDI-target expansion remain paid. Actual app/internal/direct-action admission, request/depth/continuation, and possible mutable target/attachment/child inputs remain UNKNOWN. No all-runtime unreachability or finite-graph absence upgrade.009/034 stay PARTIAL, AUDIBLE.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production, fidelity or Q15 coverage settlement.

out of scope: backing/I-O and per-sample DSP, plus census-excluded payload branches. No new family/callee descent follows the raw-reference check.
