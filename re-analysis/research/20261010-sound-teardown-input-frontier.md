# Q14-084 / Q14-118: bounded teardown caller and source-media owner frontier

Checkpoint612; scope guard reread before every step, decisions only. Native companion `20261010-sound-teardown-input-frontier-native.txt`; same engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

The specific five-entry navigation scan is saved in `20261010-sound-teardown-call-candidates.jsonl`. Twelve ARM immediate B/BL candidates reproduce PT540E's actual calls. A41C30/A41C44 are absent from the navigation index but already covered by PT540C/E; fresh bytes confirm clearD8 before A55D04(V,0), A55A84(V,secondary,1,0), require1 beforeA54A30 and its success1 beforeA56478. They are not newly discovered production paths.

Two raw words equal toA55A84 at5E8D40/44 are positively excluded as function pointers: actual Thumb5E8D18/1A loads the words,5E8D1C/20 ADD PC,5E8D22/24 dereferences resulting addresses, then5E8D26 calls4A42D0. They are PC-relative data offsets, not absolute calls toA55A84. This is positive attribution of the two candidates, not a whole indirect/relative alias absence proof.

| Mode2 destructor join | Exact decision / remaining input |
| --- | --- |
| A78C00..A78C64;actual table103DA28 slot0 | Concrete plain destructor reinstalls main/secondary tables, callsA7865C buffer release, plugin6Cnonnull Term8 with allocator,clear6C,9CF820(holder5C),thenA6BA28(source50). SF8's local field50 body is already LREL295C/D; it is not a new unread callee. Logical media release and owner callback stay decisions. |
| A78748..A788E0;A788E8 thunk | Secondary interface method subtracts14 to real source then performs actual first-key search over20-byte source50 records. Existing entry supplies raw pointer/size. Missing queries metadata by actual PBI150 pluginID, retains matching metadata under lock, obtains first sourceID mapping, releases metadata, and callsA6BAE4(source50,requestedID,mappedMediaID). Failure outputs pointer/size0. Missing mapping uses mediaID0; sentinelFFFFFFFF exits withzero outputs. |
| A6BAE4..A6BCAC | Query9BB320 writes pointer/size plus owner. Nonnull pointer required before append/reuse. Array entries are20 bytes: ID0, pointer4,size8,mediaIDC,owner10. Count increments before stores; growth by1 preserves entries. Null append/allocation releases mediaID through9B65A8 before owner.live0(owner,0), then returnsnull. Source destructor later uses precisely this owner10 via LREL295C. Not arbitrary virtual0 guessed from source class. |

PARTIAL for both original084 and118. Specific remaining census: indirect/relative aliases of the five explicitly requested entrypoints, and actual admitted mode2 source requests reaching A78748 with their sourceID-to-mediaID mapping and concrete returned owner10 destructor. Twelve direct calls, the two raw-word ambiguities, the mode2 source destructor and its record publisher are paid; no missing-caller or initially empty record-list unreachability claim. Do not reopen known codec/generator Start/Close/Term bodies.

out of scope: stage metadata/buffer/workspace/backing release internals, hash/allocator/copy bodies, PCM-file source and per-sample DSP. Counts unchanged97 native CHECKED+8 scope CHECKED/35 PARTIAL/140;140 AUDIBLE/0 INTERNAL. Research only; no production/manifest/inventory/hardware/Q15 edits.


Checkpoint655 correction: ADM570 already proves source14slot10 media requests unreachable for all46 shipped mode2 source-product lifecycles; no new source family is warranted. Actual normal queried owner101C028.0=9B47D8 is MS141/BR142 positive, supplemented by fresh native. Only original five-entry indirect/relative caller proof remains open for084/118. See `20261010-sound-mode2-media-reconciliation.md`.
