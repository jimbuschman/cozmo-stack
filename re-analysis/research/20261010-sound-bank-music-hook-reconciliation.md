# Q14-109/134: HIRC extension callback joined to installed music handler

Checkpoint761; queue5 scope guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-bank-music-hook-join-native.txt`, `-hook-producer-native.txt`, `-hook-input-native.txt`. This pays the concrete J9/MB1–3 interface within the original creator/hook question; no new musical family or Q15 completion.

| Decision step | Actual source join |
| --- | --- |
|Successful music initialization installs handler |97D77C+8+6C28D8=104005C; raw slot104005C=984930.97D780 calls9A5E64 with that value, then returns1. Existing initialized object and successful new initialization both reach the registration block97D744. Allocation failure returns2 rather than installing the handler. Preserve earlier callback registration order. |
|Actual callback field |9A5E68+8+6E79F8=108D868;9A5E6C stores unchangedR0 atbase100=108D968. This setter does not normalize pointer, inspect banks or call the handler. Other/alternate setter inputs remain separate from this positive installation. |
|HIRC header and argument origin |9B3260 preserves incomingsecondargument atSPC and third atSP14.9B3350 formsR7=SP30;9B3370 exact-reads five bytes there, non1 exits. Type byte isSP30, length isunalignedSP31. Type10..13 branch entries9B33B8/BC/C0/C4 all enterJ9. |
|Parser reloads same callback slot |9B3298+8+68CFEC=104028C;9B3EBC loads rawFFFFFE6C, selecting GOT10400F8; its raw value108D968 is then dereferenced at9B3EC4. Nonnull callback receivesR0=the same five-byte headerR7,R1=originalsecondargument,R2=originalthirdargument at9B3ED0/DC. Do not claim pointer stability across callbacks or replace the third raw argument with a guessed mode. |
|Known installed handler |984930 reads that type byte; unsigned(type-A)<=3 indexes:10 ->984AC4 segment,11 ->984B60 track,12 ->984A20 switch,13 ->98496C playlist. OutsideA..D ->984DA8 sets3 and984C1C returns it. Existing MB/MP/J source-backed creator/reader contracts own those actual selected objects. No extra creator interface is inferred from payload shape. |
|Result/skip distinction |J9 savesrawresultR4. Result3 sets continuationR6=0 and enterspayload-skip path; even a successful exact skip does not restorecontinuation. Result1 setscontinuation1 without native fallback skip; otherresults setcontinuation0 and return throughcommonlogic. Skip count unequal declared length rewritesresult7/continuation0. Preserve the null-hook path's separate skip/continuation behavior. |

This positively connects installed984930 to the actual parser call and type selection. It pays this specific HIRC hook join, not all global handler writers, constructor families, command-mode inputs or unload suppliers. The original four-word header-key writer/admitted encrypted input remains UNKNOWN; no key-zero or encrypted-unreachability inference follows from this handler.

109/134 remain PARTIAL.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production/fidelity/hardware changes.

out of scope: payload-window backing, exact-read I/O implementation, registry/hash allocation and per-sample DSP. Retain exact pointer/status/declared-length interfaces and hook selection/order; stop infrastructure descendants.
