# Q14-132: positive-context object and subscription publisher

Checkpoint611; decisions only; scope guard reread before each step. Primary instruction companion `20261010-sound-positive-context-item-producer-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Step | Primary address | Exact decision / publication |
| --- | --- | --- |
| Object type/input | 9B3380..9B33CC;9B392C..9B39F8 | HIRC byte minus1 selects branch14 at33C8 to392C. Payload's first word becomes ID. Existing item lookup uses manager78 lock,7C buckets,80 bucketcount,key8,next4; missing creates A67130(ID), requires nonnull, calls A67558(item,payload,len), requires raw1. No type inference from a similar table. |
| Fresh object | A67130..A671FC | Allocates8C, calls9D0418 with actualID, installs103D0E0, sets80/84/88 to0 beforeA66E90 registration. Bank owner retains actual pointer at9B3758..3764 after reader success. |
| Packed subscription input | A676B4..A67758 | U16 count, physical packed records. SourceID word0,type byte4,byte5, property VLQ beginningbyte6: low7 then repeated accumulated<<7 OR nextlow7 until continuation clears. Following word=curveID,byte4=scaling,U16+5=pointcount,points+7. A67384 receives these exact fields and trailing1. Require raw1 before next record; advance by12*pointcount. |
| Replacement | A672B4..A6737C | Before append, remove every record matching propertyC and curveID10; release point storage, clear14/18/1C, preserve physical order by moving later32-byte records, decrement84, recheck same position. Removal is not keyed by sourceID/type. |
| Append and failure | A67384..A6754C | Count84 increments before record stores. Record0/4/8/C/10 receive sourceID/type/byte5/property/curveID.14/18/1C startzero. Nullpoints or zero count returns1. Otherwise allocate12*count; failure returns34 with appended record retained and count unchanged; success copies points then publishes18=count,1C=scaling,returns1. Capacity growth by2 preserves physical32-byte records, then publishes88/80; array allocation/null destination failure returns2. Allocator/copy internals excluded. |
| Alternate structured publisher | A67A68..A67AF0 | Actual input count28/base2C, physical36-byte entries. Maps sourceID10,type14,byte5 field18,property1C,curveID20,scaling0,points8,count4 into sameA67384 with trailing1. First raw non1 returns immediately; otherwise reload actual inputcount before next. This establishes the publisher ABI, not a shipped caller. |
| Consumer | MC610:9BE8D0 continuation | Resolved cacheditem40 holds exactly this manager object's physical80/84 records; raw A11F98 registration1 required before next, with ctx+8 scope. Dirty/output/setup/ref gates remain MC610. Registration is retained as a decision, not excluded as coordinate arithmetic. |

Complete six-bank metadata `20261007-sound-reachability-assets.json` contains zero type14 objects. This pays the authored-bank input check only; it does not establish absence of an alternate runtime-created object or an indirect structured-reader input.

PARTIAL: the factory, object type and both concrete record publisher ABIs are now established. Remaining specific decision step: establish a shipped runtime producer for the alternate A67830 structured input (object ID and record property/type values), or a bounded proof that it cannot supply this positive-context route; then bind those actual properties to A11F98 recipients. No global caller-absence claim and no inferred empty-runtime path.

out of scope: 3D coordinate/cone/positioning-curve payload bodies, hash/backing/allocator/copy internals. No DSP or hardware claim. Counts unchanged97 native CHECKED+8 scope CHECKED/35 PARTIAL/140;140 AUDIBLE/0 INTERNAL. No production/manifest/inventory/Q15 changes.
