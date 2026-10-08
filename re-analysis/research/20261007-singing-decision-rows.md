| Q15 coverage | Status | Evidence / remaining work |
|---|---|---|
| Segment selection | NOT DONE | Positive census witness segment34452189; runtime selection and lifetime still pending. |
| Playlist selection | NOT DONE | Playlist1488599 and items985849671/921105668; runtime choice/scheduling pending. |
| Transitions, switch and meter state | PARTIAL | TP1–3 close local property55/header-or-parent-tempo selection; Switch139286641/group3759662965 transition rules remain pending. |
| MIDI dispatch and target inheritance | PARTIAL | MT1–7, MD1–6, MR1–8, RI1–5; target lookup, raw header, accumulator and local frame gates checked; full decoder/caller closure pending. |
| Note-on/off and held-note timing | PARTIAL | MH1–7, HS1–4, FC1–6; replay, release gates and frame offset conversion checked; concrete PBI virtual1C and clip readers pending. |
| Envelope parameters and trigger state | PARTIAL | MG1–8 close local trigger/factory/reuse gates; positive witness381606890/property15=2; parameter initialization, defaults and release delivery remain pending. |
| Vibrato/LFO parameters and state | PARTIAL | MG1/4/6–8 distinguish LFO trigger and allocation; witness528935089 and engine005EF184; parameter initialization and value delivery pending. |
| Approved branch exclusions | CHECKED | Five exclusions listed below with census evidence; retained adjacent controls remain in scope. |

# Q15 — singing decision rows, checkpoint 1

Request: queue5 Q15, resumed by operator. Research only; no production or fidelity changes. This checkpoint is incomplete. Addresses are from shipped `libcozmoEngine.so`, SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Raw captures: `20261007-singing-dispatch-resume-native.txt` and `20261007-singing-target-held-native.txt`. Endpoints in capture commands are exclusive. Offsets in rows are hexadecimal. Decompiler/index files were navigation only.

## Current records before comparison

> M9-004 — Music switch containers, decision trees, meters and MIDI target
> Status: IMPLEMENTATION_GAP
> Evidence: ["re-analysis/inventory/M6-wwise-bank.md", "Cozmo.bnk objects 914766641, 139286641, 602865028 and MIDI target 110896138"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. EffectiveMidiTarget starts at the segment and takes the nearest property 56; the runtime starts at the track and walks by the override bit, failing on 0 (0x00A3BDFC..0x00A3BEE4, 0x00A3CC48), and ignores the table-select flag.

> M9-005 — Singing MIDI sources use SMF division 9600 and the effective meter tempo
> Status: IMPLEMENTATION_GAP
> Evidence: ["Cozmo.bnk MIDI source plugin 0x00100001 and division 0x2580", "re-analysis/research/20260928-X4-M9-wwise-music-extraction.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the tick-to-time code (0x00A3F76C..0x00A3FBC0) and property 55 are unread; the header tempo float is dropped because the bank durations fit without it, a data fit rather than a runtime reading.

> M9-010 — Singing note-on and note-off layers determine held-note lifetime
> Status: IMPLEMENTATION_GAP
> Evidence: ["re-analysis/inventory/M6-wwise-bank.md", "Cozmo.bnk singing sampler note-on and note-off layers"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. held-note length depends on PBI vt+0x1C for code-2 entries (unread); the engine replays the Sound recorded at note-on (0x00A3E6A8..0x00A3E728), the stack makes a fresh draw; the fade question is deferred to a comment; the test is circular.

> M9-015 — Each play draws container selections afresh using Wwise's own LCG
> Status: IMPLEMENTATION_GAP
> Evidence: ["0x0098A6D4..0x0098A7B8", "re-analysis/inventory/M6-wwise-bank.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the LCG matches, but BuildVoices draws for the whole song at prewarm; the engine draws when each note fires (0x00A3EA3C, 0x00A3DDF0).

> M9-020 — A music clip uses BeginTrim and length and releases a held note at clip end
> Status: IMPLEMENTATION_GAP
> Evidence: ["re-analysis/inventory/M6-wwise-bank.md", "re-analysis/research/20260928-X4-M9-wwise-music-extraction.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. dropping notes outside the clip window and releasing held notes at clip end cite no runtime code; the end-of-PBI release (0x00A3CEB4) is read structurally only; the test is circular.

> M9-022 — Wwise container selection uses the recovered eligibility, blocked-list, random and sequence algorithms
> Status: IMPLEMENTATION_GAP
> Evidence: ["0x0098A6D4..0x0098A7B8", "0x00A08A44", "0x00A0A524", "re-analysis/inventory/M6-wwise-bank.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the note-off pass reruns every RanSeq PickIndex, moving LCG, avoid and shuffle state; the engine replays the recorded node with no selection (0x00A3E6A8..0x00A3E728).

These rows establish local contracts and confirm the recorded-node replay described in M9-010/022's unresolved. They do not establish every node callback or whole-record completion. In particular MT5 reads the table-selection flag; ignoring that flag is not faithful to this function, even though the existing M9-004 unresolved says the runtime ignores it. Both explicit lookup calls below are to the same address; the flag changes an argument, not this call's destination.

## Target property and inheritance

Positive witness: Cozmo music-switch objects139286641/602865028/914766641 carry property56 target110896138, and retain sampler children403781184/462443456/774902407 (`20261007-sound-reachability.md`, Q15 census). Scope stops at music-side calls into shared M6 node lookup.

| Step | Address | Behaviour | Gates | Order / failure results | Float bits / remaining dependency |
|---|---|---|---|---|---|
| MT1 | 00A3BDFC..00A3BE28 | Context existing34nonnull invokes its vtC, then clears context34 and30. Existing34null leaves30 untouched until MT4. | No callback-result gate. | Release→pointer clears→new lookup. | Exact ownership order. |
| MT2 | 0099239C..00992424 | Track query writes override flag=node94bit0; scans node3C property blob's count/id list for38hex (decimal56). Missing blob or absent property writes value0. Found slot offset=align4(count+4)+index*4; load raw32. Writes table flag=node94bit2. | Flag and property presence are independent; no default nonzero target. | Override→property lookup→value output→table flag. | Integer ID, not float. Blob parser is shared M6. |
| MT3 | 00987CB8..00987D30 | Parent query same property38 search, but override=node7Cbit1, table flag=node7Cbit3. Missing property returns0 while still returning independent flags. | No inherited fallback inside this helper. | Flags and value passed to caller. | These bit positions differ from track MT2. |
| MT4 | 00A3BE2C..00A3BEA0;00A3BED8..00A3BEE4 | Query track at context78, then start at track34 and query successive parent34 while current override flag0. Store final value tocontext30, final table byte to38. Final value0 clears34 and returns0. | Parentnull terminates with most recent query's outputs. Track override1 skips all parent queries. | Track→nearest-parent traversal→stored outputs→zero-target failure. | No search merely for nearest nonzero property. |
| MT5 | 00A3BE90..00A3BED4;00A3BEE8..00A3BF00;GOT0104006C | Nonzero target invokes9A7EB0(global,target,tableSelector): BE90 explicitly loads table byte into r2; flagzero retains0, flagnonzero explicitly replaces with1. Both GOT references address0104006C, rawword0108D8E0, then dereference its pointer. Store lookup pointer34. Null pointer also clears30 and returns0; success returns1. | Same function/table owner; distinct member tables in MT7. | Lookup→34store→null gate→optional30clear. | Corrects checkpoint1's unnecessary ABI uncertainty; raw word corroborated in reader-literals capture. |
| MT6 | 00A3CBE0..00A3CC60 | Context init callsA70528, saves result, initializes44 via99D5C4, calls track78.vt98(track,3). Zero result returns2. Nonzero subscribes context50 throughA19ECC(track,key-mask2000,1), then requires savedA70528 result==1; otherwise2. Success callsA1EC54(context7C,&88,&8C,&84). | Subscription happens even if saved initialization result will fail. | Init→secondary count→subscription→saved-result gate→source metadata query. | Concrete A70528/source metadata/subscription teardown still required. |

## Frame dispatch and replay

Positive witness: track11970948/media654585462/plugin00100001 and46 packed MIDI sources (census). Use raw status/offsets until the constructor establishes their ownership and units; no fabricated SMF or timestamp meaning.

| Step | Address | Behaviour | Gates | Order / failure results | Float bits / remaining dependency |
|---|---|---|---|---|---|
| MD1 | 00A3EA3C..00A3EA58 | Status byte event14 other than90/80 returns1 immediately. | Other status bypasses timing and recipients. | Status gate first. | No masking of channel nibble in this function. |
| MD2 | 00A3EA5C..00A3EA88 | Save incoming frame-step r1. Active path requires eventCnonnull, its8 wordnonnull, and incoming r2bit0set. Otherwise MD5. | Pointer presence and caller flag are all required. | Gates before release/dispatch. | Caller flag writer and exact frame units remain open. |
| MD3 | 00A3EAB8..00A3EAF0;00A3EB30..00A3EB44 | If event1Cbit1clear and signed event18 < globalu16 threshold, status80 callsA3E920; status90 does so only if byte17zero. Status90/nonzero17 bypasses that call. Then reload1C, setbit1. If priorbit1set or threshold gatefails skip this block. | Native signed BGE; no unsigned rewrite. | Optional release notification before bit1 publication. | Threshold symbol/writers unresolved, not equated to constant buffer size. |
| MD4 | 00A3EB00..00A3EB2C | When event1Cbit0clear callA3E688(event,eventC.8,eventC+C), then reload1C and setbit0. Return-bit extraction then MD5. | Dispatch once according to bit0; reload accounts for callback flag changes. | Recipient before dispatched flag. | No prewarm selection in this body; MH1 shows fresh versus replay branch. |
| MD5 | 00A3EA88..00A3EAB4;00A3EB28..00A3EB2C | Compute wrapping(event18−incoming frame-step), clamp sign-bit-set result to0, store18. Return0 when bit1clear; bit1set returns1 (bit2set explicitly forces1 through common immediate return). | Native wrap-and-sign clamp, not overflow-free mathematical max. | Decrement after optional MD3/4; gate-failed MD2 still decrements. | Exact integer scheduling. |
| MD6 | 00A3DDF0..00A3DE04;00A3DDDC..00A3DDEC | Node submission inA3DB48 first calls9F12E0(node,&request). Only result1 invokes node.vt128(node,&request). Other results go cleanup. | Node chosen by caller, not selected locally at this final dispatch. | Preparation gate→virtual dispatch→common cleanup. | Concrete vt128 choice and 9EE454 eligibility remain shared M6 dependencies; this slice alone does not prove RNG draws. |
| MH1 | 00A3E688..00A3E6A8;00A3E7E4..00A3E830 | Status90 with byte17nonzero takes fresh path: event10 node.vt10; resultB returns without submission. Other result callsA3DB48(event,node,node,incomingr1,incomingr2,&event14). Other statuses and90/zero17 take MH2. | Zero-velocity90 reaches replay path. | Type query→optional submission. | No inferred node selection before this callback. |
| MH2 | 00A3E6A8..00A3E738 | Walk event8.10 linked list physically viaentry0. Only entry4==1 participates. Copy raw event14 word; replace bytes1/2 withentryD/C. Node=entry8; querynode.vt10; typeB skips. OtherwiseA3DB48(event,node,event10,incomingr1,incomingr2,&copiedEvent). | Uses recorded node8; does not call a container picker in this loop. | Recorded-node list before PBI list. | Node virtual submission may perform its own work; no global no-RNG claim. |
| MH3 | 00A3E738..00A3E7DC | Then traverse event8.2C entries. Entry4==1 skips; other entries use PBI=entry8 and owner=PBI.E0. Null owner skips. Construct stack message and invoke owner.vtA8(owner,&message,PBI+34,1). | No result gate; next pointer loaded after callback. | Node-replay pass→PBI-owner pass. | Message words:0=incomingr1,4=copied event word with bytes1=PBI1E5,2=PBI1E6,8=PBI1E8,C=PBI1C,10=incomingr2.14,14=event18,18=3,1C=PBI. Concrete owner binding pending. |
| MH4 | 00A3E920..00A3E99C | Release pass first traverses event8.2C. For each PBI set timing: ifPBI34null storeevent18 toPBI38, else9E805C(PBI34,event18). Then call eventC.vt10. Nonzero marks entry4=3, storesPBI1F8=FFFFFFFF, callsA01280 with stack{0,4,byte0} and r2=that nonzero result. | Context callback's return—not entry type—controls this first branch. | Timing update→context query→entry/state update→recipient. | A01280 stop/update meaning and9E805C units require recipient reads. |
| MH5 | 00A3E9B4..00A3E9F4 | Context query result0: entry4==2 invokes PBI.vt1C(PBI,event10). Entry4==3 stores1F8=FFFFFFFF only if currentevent18==FFFFFFFF, otherwise0; callsA01280 withr2=1/0 respectively. Other entrytypes skip. | The second store differs from MH4's unconditionalFFFFFFFF. | Entry-type gate before recipient; physical next after callback. | Concrete PBI.vt1C remains readable and required for held-note lifetime. |
| MH6 | 00A3E9F8..00A3EA34 | After all PBIs, walk event8.48 linked entries. Call9AB1F4(global,entry8,event10,entry4), each in physical order. | Empty list returns; results ignored. | PBI list before this third list. | Global identity and9AB1F4 ownership recipient pending. |
| MH7 | 00A3DB48..00A3DBFC;00A3DD60..00A3DDC8 | Submission allocates stack request, explicitly sets gain word3F800000 and selected fields/flags; not a blanket-zeroed structure. Common cleanup9F15A0 then free optional request104 and124 arrays; release optional request2C through9A6988. | Same cleanup reached on preparation failure and after virtual submission. |9F15A0→array104→array124→retained object2C. | Full request field map/eligibility recipient remain pending; no completed constructor claim. |

## Checkpoint 2 — lookup ownership, held-note stop controls and reader timing

Companions: `20261007-singing-reader-control-native.txt` and `20261007-singing-reader-literals-native.txt`. Re-opened instructions also close MT5's selector0 argument. Existing current-record quotations apply; no manifest changes. Reader is R atA3F6E0. Its input scale is raw binary32 r1, output-list pointer r2; units remain pending initializer and caller. The complete function was captured, but the rows below deliberately do not claim its entire variable-length/system-message decoder completed.

| Step | Address | Behaviour | Gates | Order / failure results | Float bits / remaining dependency |
|---|---|---|---|---|---|
| MT7 | 009A7EB0..009A7FA8 | Selector0 locks owner0, uses bucket array4/count8; nonzero locks owner14, array18/count1C. Zero bucket count returnsnull after unlock. Otherwise unsigned division helper4A6694 computes remainder inr1 for ID/bucketcount; traverse bucket viaentry4 untilentry8==ID. Foundentry incrementsentryC wrapping32. Unlock selected lock, returnentry ornull. | Selector is table choice, not a discarded flag. | Lock→hash/search→retain count→unlock→return. | Imported lock/division execution is outside shipped code; exact shipped gates and retained entry ownership are explicit. Hash-table population remains M6 dependency. |
| HS1 | 00A01280..00A012D4 | Held-note stop recipient: PBI1BCbit7set immediately tail-calls PBI.vt0(PBI,0,0). Otherwise transition148nonnull callsA35980; nonzero takes same stop. | Request pointer is not dereferenced on immediate-stop branch. | Flags→optional transition predicate→stop. | PS1/PS2 in Q14 identify predicate/stop; full callback lifetime remains separate. |
| HS2 | 00A012D8..00A01304;00A01368..00A01380 | Set1BCbit6. Requestword0==0: PBI1BA mask78zero tail-calls vt0(PBI,0,1); nonzero checks transition144. | No requested fade invented for zero word0. | Bit6 before mask/transition decision. | Exact state; r1=0 on maskzero stop because ANDS produced0. |
| HS3 | 00A01308..00A01364 | Masknonzero/transition144nonnull updatesA366F4(manager,transition,02000000,target00000000,duration0,curve4,0). Nulltransition and incomingarg2==0 returns unchanged beyondbit6. Arg2nonzero zerosPBI168 then40; if1BDbit1clear setsbit1 and reasonbits2..4=0; thenvt0(PBI,0,0). | Distinguish arg2zero fromnonzero; do not unconditionally stop absent transition. | Transition branch before arg2 gate; target stores before reason/stop. | Target arithmetic00000000 binary32; recipientA366F4 remains M6 control dependency. |
| HS4 | 00A01384..00A013A4 | Requestword0nonzero callsA010E0(PBI,1,02000000,requestword0,requestword1,requestword2). | Bypasses zero-duration/mask branches. | Bit6 publication precedes forwarded transition request. | Exact request field meaning requiresA010E0, still pending. |
| MR1 | 00A3F6E0..00A3F730 | Save oldR48. Compute binary32 accumulator via VMLA.F32: R4C + input*R3C; floorf import; store binary32 fractional remainderR4C. VCVT.U32.F32 floor then wrapping-add oldR48, storeR48. | No finite/negative guard locally. | Accumulation→floor→fractionstore→integerclockstore. | VMLA opcode preserved; not rewritten as a binary64 accumulator. Caller/scale initialization pending. |
| MR2 | 00A3F734..00A3F768;00A3F858..00A3F890 | ExistingR24 pointer: unsigned R44>=R48 returns. OtherwiseR50bit1mustset to process. NullR24 requiresR50bit2set and unsignedR44<R48, then bit1set to process. | Equality defers processing. Two distinct flag gates. | Clock update always precedes gates. | No replacement with inclusive end-time check. |
| MR3 | 00A3F750..00A3F764;00A3F9A4..00A3F9DC | R2C highbitset/non-system status highnibble!=F0 emits. Split highnibble to status, lownibblechannel. Ifstatus90 and velocityR2E==0 replace status80. Allocate10hex bytes viaA7A7F4. Null allocation skips emission but continues decoder according toR50bit1. | System statuses and running status enter different decoder branches. | Normalize→allocate→optional event publication. | Allocation result, not arbitrary error exception; allocation helper still M6 ownership dependency. |
| MR4 | 00A3F9E0..00A3FA40 | Event bytes0=status,1=channel,2=R2D,3=R2E. Initializeword4=00000000,word8=0,wordC=0; overwrite8=R40. Offsetword4=F32(F32(u32(R44−oldR48))*R38). Append to output pair: nonemptytail=out0 writesoldtailC=new; empty setsout4=new; alwaysout0=new. | Difference wrapsunsigned32 before float conversion. No timestamp clamp. | Field writes before linked-list publication. | Exact binary32 offset arithmetic; R38 unit/initializer pending. |
| MR5 | 00A3FA44..00A3FA58 | After successful emission OR allocation failure read liveR50. Bit1clear returns; set resumes decoder atA3F768. | Failed allocation does not stop stream parsing here. | Output attempt before flag reload. | No locally meaningful success result returned. |
| MR6 | 00A3F7EC..00A3F838 | Reset branch clearsR24, sevenbytesR2C..R32, setsR28=FFFFFFFF. IfR50bit2set enter alternate reader atR18; otherwise wrappingincrementR40 by1 andR44 byR28 throughVADD.I32. | NEON instructions here update scheduling integers, not exempt PCM lane arithmetic. | Reset→optional alternate read→eventindex/clockincrement. | Branch selection into reset is still part of pending complete decoder map. |
| MR7 | 00A3F88C..00A3F958 | Alternate reader copiesR1C start intoR24, resetsR32 andR28, reads up tofour 7-bit bytes. Accumulateleft7 whilecontinuation. Fourthbytecontinuation setsR28=FFFFFFFF. NullstartsetsFFFFFFFF. | This path updates cursor before each load; no prior per-byte bounds gate. | Clear state→cursor/read→delta publication. | Parser malformation behavior is exact, not a host SMF library substitute. |
| MR8 | 00A3F958..00A3F9A0 | Alternate cursor>=R20 bypasses eventcopy. Otherwise clear sixbytesR2C..R31; nonnullcursor copiesraw32 plusraw16 tothese sixbytes. Then resume clock/index updateMR6. | Copy guarded only by cursor<end andnonnull, not six-byte remaining-size check. | Delta parse→end gate→eventcopy→index/clock. | These sixbytes are packed reader state; complete media header framing still pending. |

MR1–8 show exact scheduling and zero-velocity normalization; they do not settle property55, header tempo/division, normal decoder advance/system-status lengths, clip release, or the caller's parameter units. Those readable dependencies remain PARTIAL rather than being inferred from the bank's durations. M9-005's SMF title remains quoted as a claim to check against the source initializer.

## Checkpoint 3 — packed header, tempo source and frame caller

Primary companions: `20261007-singing-reader-init-native.txt`, `20261007-singing-tempo-native.txt`, `20261007-singing-frame-caller-native.txt`. M9-005 quoted above describes SMF; the actual reader initialization below consumes a raw six-byte header with no MThd/MTrk parsing. The asset census identifies packed media. No claim is made about every other MIDI interface; this is the source initialization reached from contextA3CBE0. Header float is used, not discarded by this production path.

| Step | Address | Behaviour | Gates | Order / failure results | Float bits / remaining dependency |
|---|---|---|---|---|---|
| RI1 | 00A3ED34..00A3ED78 | ClearR50bits0/1 before testing input. Pointerr1null or lengthr2zero returns2. Otherwise savepointerR4/lengthR8. Clearedbit0 guarantees first initialization entersA3EECC. | No length>=6 gate before subsequent sixbyte header reads. | Clearflags→inputgate→input publication→header. | No inferred safe short-input handling. |
| RI2 | 00A3EECC..00A3EEF4;00A3EF74..00A3EF78 | ZeroR14/C/10 andsetR50bit0. Readdivision=(byte0<<8)|byte1, storeR14. Division0 takesbit1clear failure pathRI5. | No chunk magic/header length parsing here. | Stateclear→flagbit0→divisionread→zero gate. | Division2580hex=9600 is asset value, not reader constant. |
| RI3 | 00A3EEF8..00A3EF70 | Assemblebytes2..5 little-endian raw32 andstoreR34. Interpret asbinary32 tempo. Compute productF32(tempo*F32(signeddivision)); R38=F32(476A6000/product), R3C=F32(product/476A6000). SetR20=pointer+length wrapping32,R1C=pointer+6,R50bit1; zeroR18byte,R24,R28,R32. | No zero/NaN/finite tempo gate. | Headerread→rawtempo store→product→cursor/end/flags→ratio stores. |476A6000=60000. All arithmetic binary32; exact scheduling. |
| RI4 | 00A3ED98..00A3EEC8 | Successfulheader entersinitial delta reader atR1C. Parseup tofour7bitbytes, advancingR24; fourthcontinuation setsR28=FFFFFFFF. On cursor<end, clear six event bytes and copy raw32+raw16 when nonnull. Successful event-copy path sets ip=1; cursor>=end bypasses that assignment. | Perbyteboundschecks absent. Cursor>=end skipscopy butdoesnotlocally failheader. | Header→delta→eventcopy→RI5 common publication. | Complete normal decoder still pending; initial raw packed-copy gate explicit. |
| RI5 | 00A3EE4C..00A3EE7C;00A3ED7C..00A3ED94 | PublishR44=R28,R40=0,R4C=00000000,R48=0; insertipintoR50bit1. Setbit1returns1. Clearbit1clearsR4/R8 andreturns2. Headerdivision0 arriveswithip0, thusfailure. | Fourth-byte continuation or cursor>=end arrives with ip=0 and returns2; cursor-null path reaches ip=1 with sentinel FFFFFFFF. Preserve the actual path, not a blanket sentinel-to-error rule. | Clock reset→validflag→return orinputclear. | Explicitbinary32zero fractional accumulator. |
| TP1 | 00992428..00992498;00987D34..00987DB4 | Property37hex(decimal55) query scansblob3C asMT2. Trackoverrideflag94bit1; parentoverride7Cbit0. Missingblob/propertyvalue0. | Overrideandvalueindependent. | Queryflag→propertyraw32output. | No boolean conversion untilconsumer. |
| TP2 | 00A3C8F0..00A3C968 | Querytrack78, thenfollowparent34whileoverrideflag0 andparentnonnull. Finalrawproperty55nonzero returnsraw00000000 tempo throughinitializedstackword8. Property55zero entersTP3. | Compareflagagainstnormalized(parentnonnull) viaunsignedBHS/BLO; equivalentwalkgatefor0/1flag. | Track→propertyoverridewalk→valuegate. | Nonzero55 meansno externaltempooverride, allowingRI3 headerfloat. |
| TP3 | 00A3C96C..00A3C9B4;00987DB8..00987DD4 | With55zero, starttrackparent34; followparentsuntil7Cbit2set ornoancestor. Eachqueryoutputsrawnode68 onlywhenbit2set, otherwise0. Returnrawbinary32tempo; noancestorreturns0. | Meterlookupstartsparent,nottrack. | Separate property walk beforemeter walk. | Node68 andoverridebitwriters/serializedmeterreader remain dependencies. |
| TP4 | 00A3EF80..00A3EFB4;00A3CD8C..00A3CD9C | CallerTP2/3 valuepassedtoA3EF80(reader). Inputexactzero usesstoredR34; nonzeroincludingNaN replacesR34. MultiplyF32(u32R14)*tempo; recomputeR38/R3C with476A6000. | No tempo clamp. | Sourcemetadata/init precedesthisoverride inA3CBE0. | Zero isretain-headertempo sentinel,notzerotempooverride. |
| TP5 | 00A3EFBC..00A3EFC8;00A3CDFC..00A3CE10 | SetR50bit2fromcallerlowbit. CallerA1E280 result1passes0; allotherresults pass1. |No genericresultsuccessgatehere. |Property3A/randomquery→bit2writer. |X2 inQ14 coversA1E280; loopinputmayconsume RNG. |
| FC1 | 00A3C4B8..00A3C4F4 | ContextFCbit1set returnsbeforeA708C0. OtherwisecallA708C0(context), saveinputr1/r2. |No clockworkonreturnbranch. |Gate→recipient→localtimecalculation. |A708C0 remainsreadablerecipient. |
| FC2 | 00A3C4F8..00A3C538 | SaveoldcontextF4, wrappinginputr2−oldF4; clearF4. Convert differenceassigned32toF32. Duration=F32(F32(signeddifference)/F32(F32(globalu32)/447A0000)). Zero durationtakesFC3; nonzeroincludingNaNcallsreader(context90,duration,&emptylist). |No nonnegativeclamp. |F4clear→durationarithmetic→zero gate→reader. |447A0000=1000. Globalrateidentity/writerspending; denominatorvaluesareinputsnothostassumptions. |
| FC3 | 00A3C53C..00A3C57C | ContextF8==FFFFFFFF callsA708D4 once. OtherwiseclearF8; ifFCbit2clear,setbit2andcallA708D4, thencallA708D4 again. Alreadybit2setcallsonce. |Two calls onthefirstclear-bit2 branch areliteral controlflow. |F8clear→optionalflag/callback→callback. |A708D4 recipientpending; donotcoalescecallbacks. |
| FC4 | 00A3C608..00A3C658;00A3C6E8..00A3C72C | Popheadentryfromgeneratedlistbeforeprocessing. Convertentry4binary32tomilliseconds/sampleindexthroughbinary64: x=F64(entry4)*F64(u32global)/408F400000000000; add+3FE0000000000000onlyx>0,elseBFE0000000000000;VCVT.S32.F64. |x0usesnegativehalf;NaNalsofailsGT. |Unlink→timeconversion→status/windowgate. |Binary64roundingandbinary32inputwidthareexactscheduling; noMath.Round substitute. |
| FC5 | 00A3C5B4..00A3C604;00A3C650..00A3C678 | F8!=FFFFFFFF path: status90/nonzerovelocityandunsignedF8<=convertedindexskipA70738. OtherentriescallA70738(context,inputr1,entry,index+oldF4,entry8,FCbit2,FCbit3), thenreloadFCclearbit3. AllentriesfreethroughA7A988. |SkipbranchdoesnotclearFCbit3. |Gate→optionalcall→optionalflagclear→free→next. |ExactclipendinterpretationawaitsF8writer; localunsignedboundexplicit. |
| FC6 | 00A3C67C..00A3C740 | F8==FFFFFFFF path: status90/nonzerovelocityskipsrecipientonlywhensnapshotFCbit2set. Otherentriesdispatchsameargsandclearbit3aftercallback. Allentriesfree; emptylistA708D4. |UsesFPsnapshotcapturedbeforethereader,notlivebit2forthisskipgate. |Reader→snapshot-gatednoteon→liveflagsforwarded→free→finalcallback. |A70738/postingrecipientandclipwritersremainrequired. |

This closes the local header-tempo/property55 questions that were explicitly unread in M9-005; it does not settle the whole M9-005 path, because full normal decoding, seeking and caller closure remain. The exact distinctions between binary32 accumulation and binary64 event-offset rounding are recorded, including zero/negative/NaN branches.

## Checkpoint 4 — modulator trigger and allocation controls

> M9-006 — HIRC LFO and Envelope payloads and their runtime classes
> Status: IMPLEMENTATION_GAP
> Evidence: ["0x009D7B6C..0x009D7C97", "vtable 0x0103B1E8 and 0x0103B218", "re-analysis/research/20260928-I-M9-gap1-extraction.md"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. property 15 is the trigger selector (0x009D552C..0x009D55F0), not stop-playback; the stop gate is property 1 (0x009D7EA4, default 1), unmodelled; the test keeps the wrong name.

> M9-008 — The vibrato LFO binding depth is driven by the posted cube-shake parameter
> Status: IMPLEMENTATION_GAP
> Evidence: ["0x009D671C..0x009D7727", "0x005EF184..0x005EF18C", "Cozmo.bnk objects 528935089 and 110896138"]
> Unresolved: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. the depth RTPC is evaluated by the per-voice initializer only; the stack re-reads it every block, an uncited behaviour its test asserts.

> M9-024 — Whether the note-off envelope stops the voice it is attached to
> Status: RECOVERABLE_GAP
> Evidence: ["0x009D552C..0x009D55F3", "0x009D5934..0x009D6598", "0x009D7FC0..0x009D8137"]
> Unresolved: Trace the type-22 property-15 boolean from virtual method 0x009D552C through the per-voice object and identify whether it stops the attached note-off voice.

> M9-025 — The exact waveform produced by the Wwise LFO between its extrema
> Status: RECOVERABLE_GAP
> Evidence: ["0x009D671C..0x009D7727", "0x009D7FC0..0x009D8137", "0x009E266C..0x009E2813"]
> Unresolved: Follow the type-21 per-voice object created at 0x009D7FC0 through vtable 0x0104B268 and map reads of LFO state +0x34..+0x48 to the waveform sample equation.

Primary companions: `20261007-singing-modulator-gates-native.txt` and `20261007-singing-modulator-defaults-native.txt`. Default-property table0108DAA8 is runtime storage outside file-backed LOAD bytes; this capture does not establish its post-constructor values. This is not a phone-runtime classification: its shipped initialization still needs tracing. M9-024's property15 wording is contradicted locally by MG2/3; the whole attached-voice stop path remains open.

Positive retained witnesses: envelope381606890 bound462443456 (property15=2, property9=00000000,10=41C00000,11=00000000,12=41180000,14=00000000); LFO528935089 bound110896138, external RTPC C20F49DF (properties0=1,2=42C80000,3=3E4CCCCD,4=40B00000,7=42480000), from the reachability census. These are bank values, not yet a claim that every runtime parameter equals the serialized base after RTPC/random processing.

| Step | Address | Behaviour | Gates | Order / failure results | Float bits / remaining dependency |
|---|---|---|---|---|---|
| MG1 | 009D7B6C..009D7C94;0103B1E8..0103B23C | Factory selector0/1 allocates48hex bytes via A7A7F4. Null allocation or other selector returnsnull. Invoke9D0418(object,incomingID); selector0 installs vtable103B1E8, typeword10=0; selector1 vtable103B218,typeword10=1. Both zero words14/18/1C/24/2C/30/34/38/3C/40 and bytes28/44, install secondary vtable20; call9D790C before returning pointer. | Does not blanket-zero all48 bytes. | Allocate→base constructor→concrete fields→9D790C→return. | Secondary vtables/base constructor and payload reader9D790C remain readable dependencies. |
| MG2 | 009D552C..009D5588;0103B23C | Envelope vt24 reads raw propertyF(decimal15) from blob14, or defaultword0108DAA8+3C if absent. Same count/id/aligned-values blob search as MT2. | Property15 is a selector value, not normalized boolean. | Property/default query before event gate. | Actual runtime default writer remains pending. |
| MG3 | 009D5588..009D55F0 | Incoming eventbyte4==0 returns1 iff selector==1. Eventbyte4nonzero: selector1 returns1 iff eventword18!=3; selector2 returns1 for status80, or status90 with byte7zero; all other selectors/statuses return0. | Selector1 does not require a particular nonzero MIDI status. | Event gate→selector branch→word/status/velocity tests. | Exact trigger decisions; no stop callback here. |
| MG4 | 0103B20C;009D5070..009D50A0;0103B208/0103B238;009DE688..009DE694 | LFO vt24 ignores property15: eventbyte4==0 returns1; status90 returns normalized(byte7!=0); otherstatuses0. LFO vt20 returns0; envelope vt20 returns1. | LFO accepts nonzero-velocity note-on, envelope selector2 accepts note-off/zero-velocity note-on. | Separate concrete bindings before common factory consumer MG6. | No oscillator/sample arithmetic read. |
| MG5 | 009D7EA4..009D7F34 | Typeword10==0 returns0 immediately. Other types query property1 in blob14 or default0108DAA8+4; return normalized(rawvalue!=0). | Type0 LFO bypasses property1 entirely. | Type gate→property/default→boolean. | Confirms separate stop gate; default1 is existing record claim still awaiting constructor check here. Caller9E21FC remains required. |
| MG6 | 009D7FC0..009D8034;009D80A0..009D80B8 | Set caller's created-output byte0 before invoking object's vt24(object,event). False returns0, even with existing state. True/existingstate: invoke vt20. If nonzero and `(eventword18 & ~2)==1`, call9E266C(existing,object,event,arg3); otherwise return existing unchanged. | Bit-clear test accepts word1 or3 only. | Created=false→trigger→existing gate→optional reinitialization→return existing. | Reinitialization recipient remains exact parameter/state work, not presumed DSP. |
| MG7 | 009D8038..009D8084;009D80BC..009D80DC;009D7F3C..009D7FB8 | True/noexistingstate queries property0 (or default0108DAA8). Nonzero property0 and eventword18==0 returnsnull. Otherwise type0 allocatesAC, type1 allocates8C; othertype or allocationnull returnsnull. | Absence of existing state does not guarantee creation after accepted trigger. | Property gate→type→allocation. | Defaults/shipped property0 writer, scope meaning and allocator recipient pending. |
| MG8 | 009D8088..009D809C;009D80E0..009D8134 | New state invokes9E2144, installs concrete per-voice vtable. Type0 additionally zeroeswords90/94/98/9C/A0/A4 and byteA8. Then9E266C(new,object,event,arg3), ignore result; set created-output byte1 after call and returnnew. | Created flag is after parameter initialization, not after allocation. | Base state→concrete state→initializer→created flag. | Concrete per-voice vtables, parameter initializer and downstream stop/value delivery remain pending. |

No sample equation was extracted. The trigger, reuse, scope and allocation gates remain exact, while parameter randomization/RTPC application and lifetime recipients are still readable work. Q15 remains incomplete, with segment/playlist coverage still NOT DONE.

## Approved excluded branches

| Branch not extracted | Census evidence | Retained work |
|---|---|---|
| 3D positioning bank payload | Position bytes00/C0/C3 fail overridebit0+bit3 gate009ECF6C..009ECF9C. | Common prefix/runtime routing. |
| Nonempty blend tracks | All six type9 objects have count0. | Child order, continuous flag and MIDI child filtering. |
| RanSeq bit5 ping-pong | All468 flags12/1A havebit5clear. | Remaining RNG/sequence/loop decisions. |
| Zero/multichannel file sources |2214 externalWEM and embeddedRIFF headers channels1/2. | Mono/stereo geometry and failure handling. |
| PCM file sources | Format tags2/FFFF; no1. | ADPCM/Vorbis/generated sources and shared lifecycle. |

Evidence: `20261007-sound-reachability.md` and raw `20261007-sound-reachability-assets.json`; operator adopted these branch exclusions. No other negative reachability inferred. No PCM expectations or oracle claims are made by this instruction extraction.

Remaining work is explicit in the coverage table and recipient columns. Q15 is not finished. Candidate hosts are existing `WwiseMusic.cs` for target/context, `WwiseMidi.cs` for dispatch/timing, and `WwiseSongRenderer.cs` for held-node/PBI ownership; these are placement suggestions, not authorized builds.
