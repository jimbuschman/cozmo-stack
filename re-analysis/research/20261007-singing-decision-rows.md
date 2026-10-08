| Q15 coverage | Status | Evidence / remaining work |
|---|---|---|
| Segment selection | NOT DONE | Positive census witness segment34452189; runtime selection and lifetime still pending. |
| Playlist selection | NOT DONE | Playlist1488599 and items985849671/921105668; runtime choice/scheduling pending. |
| Transitions, switch and meter state | NOT DONE | Switch139286641/group3759662965; runtime transition rules pending. |
| MIDI dispatch and target inheritance | PARTIAL | MT1–6, MD1–6; concrete dispatch, target-property flags and local frame gate checked, context/source and timing recipients pending. |
| Note-on/off and held-note timing | PARTIAL | MH1–7; physical replay and PBI notification gates checked, concrete PBI virtual1C and clip/time readers pending. |
| Envelope parameters and trigger state | NOT DONE | Positive witness381606890/property15=2; native construction/trigger/release recipients pending. |
| Vibrato/LFO parameters and state | NOT DONE | Positive witness528935089 and engine005EF184; native value delivery/trigger recipients pending. |
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
| MT5 | 00A3BEA4..00A3BED4;00A3BEE8..00A3BF00 | Nonzero target invokes9A7EB0(global table,target). Table flag nonzero explicitly passes r2=1; flagzero branch does not locally assign r2 before call (helper output register is not assumed). Store lookup pointer34. Null pointer also clears30 and returns0; success returns1. | Destination9A7EB0 is shared, not two named tables inferred from notes. | Lookup→34store→null gate→optional30clear. | Precise flagzero ABI meaning and global table identities pending callee/relocation check. |
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
