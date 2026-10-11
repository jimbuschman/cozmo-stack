# Q14-033: positive voice policy writer frontier

Checkpoint650; request `requests/20261007-codex-queue-5.md`; scope guard reread before each step; decisions only. Primary `20261010-sound-voice-policy-writer-attribution-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Four bounded immediate-store candidates retained in `20261010-sound-voice-policy-store-candidates.json`; constructor and input gates retain their existing SA203/VP3/PC2 native companions.

| Positive writer | Exact native identity and order |
| --- | --- |
| A557A8/A557AC | Current argument r0 is voice, saved r4 atA55758. Primary D4 supplies PBI. After context24/28 and PBI1BE&14 gate, A0275C nonzero selects E4=2 then E0=1. Queryzero reads current E4: zero selects the same writes; nonzero preserves both. The E0 store isA557AC, after the olderVP3 first slice's A557A8 boundary; fresh capture explicitly contains it. |
| A559D0 plus aliased E0 | New-source path usesA559C0 r1=voice+E0, A559C4 A01768(PBI,&E0), then stores returned raw policy intoE4 atA559D0. Query/outword precede route-capacity test/allocation. Incoming controlarg3 zero skips both; nonzero takes the query, not merely bit0. Policy/mode publication remains even if later route allocation/start fails. |
| A55B60 plus aliased E0 | Retained-source path likewiseA55B50 r1=voice+E0, queryA55B54 then store policyE4 atA55B60. SA203B exact gate is bitwise incomingarg3 ANDarg4, not logical “both nonzero”. KnownVS4 inputs1/0 preserve old mode/policy. Neither this path nor constructor silently supplies fresh zeros. |
| Output-pointer dependency | PC2 A01768's cache-valid path writes cached low3 bits into caller's output word; cache miss delegates inheritedPC1, then caches policy/output. Thus E0 writes are call-mediated and positively aliased by these two callers. Counting only immediate STR E0 would omit them. |

The bounded A53000..A55C10 aligned ARM STR/VSTR scan finds those four immediate encodings; it deliberately excludes no other writer merely by absence. Relative offsets, STM/copies, other method regions, computed pointers and caller-mediated updates still require the original complete writer/caller proof. All four candidates here have concrete voice identity; no surrounding unrelated storage/DSP body is descended into.

Q14-033 stays PARTIAL for explicit indirect VoiceInit/re-init caller proof and live E0/E4 writer aliases beyond these positive three paths. The new/retained/virtualizing producer distinction and source-defined preserved/unassigned cases are paid; source constructors are not reinterpreted as zero-initialization. Existing TH/DF/VS/DA498/DT499/TM484/LREC/RSRC controls remain exact.

out of scope: allocation/backing/I-O, positioning payloads and per-sample DSP. Counts105 native+8 scope CHECKED/27 PARTIAL;140 AUDIBLE/0 INTERNAL. Research only; no production, manifest, inventory, hardware or Q15 change.
