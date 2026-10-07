"""Write Q4's current manifest quotations ahead of its independently checked findings."""
import json
from pathlib import Path
m=json.loads(Path('re-analysis/fidelity_manifest.json').read_text(encoding='utf-8'))
rows={r['id']:r for r in m['records']}
coverage='''| Review item | Coverage | Scope / remaining boundary |
|---|---|---|
| B-ACTIONS queue/tick 6539e66; synchronization follow-up298ec71 | CHECKED | ActionList/ActionQueue, rejection/deletion and tick iteration. |
| Runner lifecycle bb8c9d5; global counter2666e70 | CHECKED | Init/timer/retry/lock/destruction surface; tag-release omission below. |
| Compound construction/order/failure6639fd4 | CHECKED | Common and sequential/parallel row families, including reset and interrupt slots. |
| Watcher/completion/game/mood registration8e6c719 | CHECKED | Local watcher and serializer; higher-layer emotion recipient remains an interface. |
| B-FACE e0b2103 | CHECKED | Desired-degree parser/getter and live failure write; same-implementation expected RNG values flagged below. |
| Claim/job/status-only matching commits | CHECKED | 0100a8e,1398e06,b0c10a7,18ea788,7f552d2,5b86b68,f876450; no behavior diff. |
| Concrete batch3b child-action recipients | PARTIAL | Job explicitly blocked; not built by these commits and not expanded into upper layers. |
| Engine-to-game delivery sink / configurable 0x198 producer | PARTIAL | Explicit existing gaps, not asserted recovered by this review. |

Q4 of `requests/20261006-codex-queue-3.md`. Pulled main; reviewed through `6f13a5b` (production code unchanged from `b4ce02b`). Defects and circular tests only. No implementation, inventory or manifest edits. Findings below identify actual omissions/contradictions rather than restating a known absence of an app transport or a fixed ProceduralLive producer. Companion `20261006-B-ACTIONS-B-FACE-native.txt` reopens the cited native paths in the shipped engine. SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

## Current manifest quotations before findings

'''
out=[coverage]
for ident in ('M4-003','M4-016','M7-020','M7-017','M8-001'):
 r=rows[ident]
 out.append('```json\n'+json.dumps({k:r.get(k) for k in ('id','title','status','authority','location','evidence','unresolved')},ensure_ascii=False,indent=2)+'\n```\n')
out.append('''## DEFECTs

| Finding | Engine behavior and reopened instructions | C# file:line / observable consequence |
|---|---|---|
| A1: reserved tags survive runner destruction | `0x005410FA..0x00541114`: erase current `+0x60` then original `+0x5C` from the global in-use tree, **before** track stop/unlock and watcher enqueue. Constructor reserves and SetTag collision checks use that same tree. | `cozmo-stack/src/Cozmo.Robot/Actions/IActionRunner.cs:431`: WatcherEnding stops/unlocks/enqueues but releases neither tag. Its sole Global.Release call is SetTag at387. After action deletion, a new action cannot reuse the released external tag: C# returns BAD_TAG `0x03000006` where native uniqueness permits it. This is a lifecycle omission, not an OS policy. |
| A2: rejected actions lose their watcher destruction events | Native QueueAction rejection `0x0053D972..0x0053DA9A` deletes via key0 queue; base runner already owns Robot `+4` from ctor `0x0053FDDA`. Destructor `0x00541230..0x00541238` follows Robot+0x250+0x10 to ActionEnding regardless of game interface. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionList.cs:158` discards **before** Watcher is assigned at167. `ActionQueue.cs:239` obtains watcher from that still-unbound runner, and `IActionRunner.cs:441` only invokes Watcher?.ActionEnding. A newly constructed ActionRunner with BAD_TAG, or an external tag while ExternalActionsDisabled, therefore creates no completion-to-watcher event; native does. The fake-action Q2 test checks deletion/map occupancy, not this recipient. |
| A3: queue current/list ownership is cleared after broadcast | `0x0053FA78` invokes destructor; `0x0053FA7C` nulls caller runner reference; `0x0053FA86..0x0053FA9C` unlinks pending node/decrements count; **then** `0x0053FAAA..0x0053FAB2` constructs/broadcasts completion. Guard-tag erase remains after broadcast. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionQueue.cs:242` broadcasts inside DeleteRunner; callers null current at85/101/111/174/203, or unlink pending, only after it returns. A synchronous game sink inspecting GetCurrentAction/Pending sees the destroyed runner still owned, unlike native. Reentrant QueueAction/Cancel also sees a different current/pending topology. The code implements “destructor before broadcast” but omits the intervening null/unlink steps. |
| A4: queue erasure and traversal are delayed/invalidated | `0x0053F5A8..0x0053F5D6` checks and erases each empty queue **inside** the tree traversal; erase returns next iterator. Nonempty successor is found from live tree links `0x0053F5B2..0x0053F5CC`. No vector/snapshot or version check occurs. | `cozmo-stack/src/Cozmo.Robot/Actions/ActionList.cs:114` uses a fail-fast SortedDictionary enumerator and collects keys for later erase at118..124. A later runner callback observes an earlier empty parallel key still occupied and allocates a different lowest free key. A callback that inserts an IN_PARALLEL queue invalidates the enumerator, producing InvalidOperationException on continuation instead of native live-tree traversal. The reentrant lock does not stop same-thread callbacks from inserting. Completion callbacks execute within UpdateInternal before traversal continues (`0x00540EEA..0x00540F20`). |
| A5: completion union replaced with arbitrary four-byte wire/cache value | Native record owns a40-byte tagged ActionCompletedUnion at+0x18. `0x00715E7A..0x00715E80` calls that union's Pack; `0x0075C8E0..0x0075C8EE` writes **one tag byte**, then `0x0075C8F2..0x0075C944` dispatches variant payload packing. DefaultCompleted constructor `0x0075C838..0x0075C83C` sets tag6; shipped `unity/scripts/csharp/Anki.Cozmo/DefaultCompleted.cs` has zero-byte payload. INVALID likewise has no dispatched payload. Base GetCompletionUnion `0x0052B0A6..0x0052B0AE` copies the cached union via its recipient, not a four-byte integer field. | `cozmo-stack/src/Cozmo.Robot/Actions/RobotCompletedAction.cs:41` writes U32(CompletionUnion), while `IActionRunner.cs:160` stores a uint cache and compounds cache/proxy that uint. For DefaultCompleted the native union bytes are `06`, not `06 00 00 00`; for INVALID they are `FF`. A named UNKNOWN variant does not authorize emitting a guessed scalar. The existing transport sink is still missing, so this wire defect is currently in the built packer rather than a demonstrated robot/app capture. |

The A4 callback-insertion case is a specific missing branch, not a claim that any arbitrary native callback mutation (especially deleting its own iterator) is safe. The native tree walks cited establish insertion and earlier-key erasure behavior; no new safe-mutation policy is inferred.

## Circular or unsupported tests

| Test / line | Why it cannot verify the claimed source behavior | Independent source expectation |
|---|---|---|
| `cozmo-stack/tests/Cozmo.Protocol.Tests/ActionCompletionTests.cs:413`,420,435 | Expected lengths and trailing four bytes are chosen from the new uint-cache design, despite the comment claiming D8/D9. The test at408 accepts DEADBEEF as a “union”; no shipped variant has been established for that value. This is an unsupported design baseline in addition to A5. | Native tagged pack `0x0075C8D8..0x0075C946`: one byte tag then variant payload. DefaultCompleted tag6 and zero payload (native ctor above / shipped generated class). With two subresults a default body length is22, not25; with256 results it is1038, not1041. |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DesiredFaceDistortionTests.cs:53..55` (assertion55; callsites79,99,134,155,168,262) | Expected RNG state comes from a fresh instance of the same EngineRandom implementation. This can test relative consumption but a shared RNG defect passes both sides; it is not an independent native expected-value oracle. | `0x0063B804..0x0063B884`: successful sample consumes degree draw then cooldown draw; each GetNextDbl consumes two MT words. A blocked/cached getter consumes none. Use native-produced next-word/bits fixtures, not this class on both sides. |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DesiredFaceDistortionTests.cs:216..220`,226..227,246..258 | Sampled degree/deadline expected values use the same production EngineRandom seed/draw code. Writing the native bound formula around that generator does not independently establish its output. | Bounds and sample storage are binary32 at `0x0063B804..0x0063B83C`, `0x0063B840..0x0063B884`, `0x0063B94C..0x0063B954`; draws use binary64 `0x0082FA48..0x0082FA72`. Expected sampled bits need captured/emulated shipped RNG+getter results. This review does not fabricate those bits by running the C#. |

No new regression code was added in the research lane. These omissions require an independent corrected build and checked source-derived expectations; passing3946 existing tests does not remove them. PARTIAL boundaries in the coverage table remain explicit and do not hide any unexamined task as finished.
''')
Path('re-analysis/research/20261006-B-ACTIONS-B-FACE-review.md').write_text('\n'.join(out),encoding='utf-8')
