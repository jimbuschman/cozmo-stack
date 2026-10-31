# ChatGPT queue 6: Claude check (manager, 2026-10-31)

**Verdict: quotes and addresses held, and its address rule is now right. It left too many rules unfinished: action constructors without
their values, and payload fields as UNKNOWN that the CLAD classes name. No correction reverses a behaviour it stated except the labels
below. Nothing settled; no status changed.** The report is `research/20261031-queue-6-chatgpt-extraction.md`. Use this note where the
two differ. All seven items were reported Partial, honestly. A row not named here was not re-read.

## Corrections

**1. Row 2.04: the base is the list node, not TrackedPet.** r8 walks `std::list` nodes and the payload starts at node+8 (row 2.02). So
the timestamp is TrackedPet +0x0C and the mode byte TrackedPet +0x11. Map node +0x14 is TrackedPet +0, which agrees with node+0x20
being the timestamp and node+0x25 the same byte.

**2. Item 2 payload table: every field has a CLAD name.** From `RobotObservedPet.cs` (size 33, matching the report):

| Payload | Field | Source |
|---|---|---|
| +0 | petID | map node +0x14 (not a timestamp) |
| +4 | timestamp | node +0x20 |
| +8 | numTimesObserved | node +0x18 |
| +0xC | score | node +0x1C |
| +0x10 | img_rect | node +0x28..+0x37 |
| +0x20 | petType | node +0x24 |

**3. Row 3.05 and ImageChunk table.** Slot 0 returns the channel count: Vision::Image 1 (0x005643F2), ImageRGB 3 (0x006A8958). So encoding
is 5 JPEGGray for one channel, otherwise 6 JPEGColor. CLAD names the "auxiliary" fields: +8 `chunkDebug`, +0x10 `status`. Resolutions 4,
5, 6 are QVGA, CVGA, VGA. The dimension test is rows then cols (0x0064F7D2..0x0064F862).

**4. Row 3.14 misses the behaviour.** The counter is not only a diagnostic. On a completed image: a new timestamp resets +0x128 and
stores it; a repeated timestamp increments it. From the third repeat on, the code warns and **skips
`VisionComponent::SetNextImage`** (0x00535B50..0x00535B9A). So the rule in row 3.11 and this drop share one counter.

**5. Row 4.08: 0x03000009 is not an action tag.** It is ActionResult INTERRUPTED, read from action +0x18 (the stack already names it,
`IActionRunner.cs:15`). The tag is +0x60 (0x0053E84A). Rule: an action whose result is INTERRUPTED sends no RobotCompletedAction.

**6. Row 7.01 and the item 7 rule: +0x84 is not a fallback flag.** It is the behaviour's live action tag: StopActing passes it to
ActionList::Cancel (0x005BD3DA..0x005BD3F0). The rule is: runnable while carrying, or while it has a live action.

**7. Item 1 rule.** Row 1.05 is not a "head movement and lift movement" outline only; see below. The two closures are success and
failure in that order: 0x01029068 at r3, 0x010290A8 on the stack (0x005DC27A..0x005DC298).

## Closed beyond the report

- **Item 1, helper failure callback (0x005DC818..0x005DC976):** when not carrying, set +0x15C and StartActing an action with an empty
  callback. The action is a CompoundActionSequential of:
  1. CompoundActionParallel[DriveStraightAction(-20.0, 20.0, false), MoveHeadToAngleAction(0.4363 rad, tol 0.0349, 0),
     MoveLiftToHeightAction(0, 5.0, ...)]
  2. WaitAction(1.0)
  3. MoveHeadToAngleAction(0.2618 rad, tol 0.0349, 0)
- **Item 1, Container::Update (0x006168EC..0x006169C4, all three containers):** slot +8 copies the cache (+4/+8) to +0x10, slot +0x10
  ClearCache. For each located object not yet in the seen set: slot +0x14 AddAllConfigsWithObjectToCache, then add each returned
  config's object IDs to the seen set. Slot +0xC then releases the +0x10 copy. The copy is why CastNewEntry can reuse old entries. Slot
  +8's veneer target was not named; ConfigurationType values are not named.
- **Item 3:** a robot chunk needs the first chunk to set the image ID, as the report says. CompressAndSendImage's callers were not read.
- **Item 4:** the six PLTs are StartActing overloads, as it says, so its UNKNOWNs are honest; queue 3 showed the callback fires on any
  result. The census remains M8-024's open work.
- **Item 5, PounceOnMotion (0x005F9C58..0x005F9E92):** with area = msg+0xC and threshold +0x120: if area > threshold, +0x16C = now. Only
  in state 6:
  - area > threshold and hypot(msg+0x10, msg+0x12) <= +0x11C: +0x12C = now, +0x188 = 1, +0x130 += 1, +0x134 = distance, store msg+8 and
    msg+0xA at +0x160/+0x162, +0x164 = 1, then StopActing(true, false).
  - Otherwise, if now >= +0x12C + +0x124 and +0x130 >= 1: +0x130 = 0.
- **Item 5, the M15-069 question closes for this site: yes, it completes synchronously.** StopActing (0x005BD34C) cancels tag +0x84 via
  ActionList::Cancel. ActionQueue::Cancel(tag) (0x0053E83C) calls IActionRunner::Cancel and DeleteActionAndIter on the match. So a
  live action completes inside the RobotObservedMotion handler. ActionList::Cancel(tag) 0x0053E704 was not read; the link to the queue
  is assumed from its name.
- **Item 5, all six positions (table 0x0053DA2E):** NOW QueueActionNow; NOW_AND_CLEAR_REMAINING duplicate check, Cancel(-1), then
  QueueActionNext; NOW_AND_RESUME QueueActionAtFront; NEXT QueueActionNext; AT_END QueueActionAtEnd; IN_PARALLEL AddConcurrentAction.
- **Item 6:** the events are `Play__Coz_App__Enter_Background` (0x783BE1EC) and `Play__Coz_App__Enter_Foreground` (0x490FE02B), from
  Unity `GenericEvent.cs`. So not focused posts Background. No later Wwise effect was read.
- **Item 7, the rest of the run:**
  - Trigger 410 is PutDownBlockPutDown; DriveStraightAction is (robot, RandDblInRange(-45, -75) mm, 100, true).
  - IncreaseScoreWhileActing(5.0) before each StartActing.
  - CreateLookAfterPlaceAction(robot, true): a CompoundActionSequential. **Only if still carrying:**
    CompoundActionParallel[MoveHeadToAngleAction(-0.3491 rad, tol 0.0349, 0), DriveStraightAction(-30)], then
    WaitForImagesAction(2, VisionMode 1, 0). Then, since the argument is true: TurnTowardsFaceWrapperAction wrapping
    TriggerAnimationAction(PutDownBlockKeepAlive (409), 1, true, 0, 60, false), with angle pi.
  - The final closure, if still carrying, warns and then calls `CarryingComponent::SetCarriedObjectAsUnattached(false)`
    (0x005C8480..0x005C84CE). An earlier version of this note said it only warned; that was a short disassembly window. Found during
    ChatGPT check 1.
  - Reaction table 0xC68B20 is 21 (trigger, flag) byte pairs, triggers 0..20, every flag 0. What flag 0 means in
    SmartDisableReactionsWithLock was not read.

## Test-expectations trial

Partly useful. It correctly refused to invent a wire trace, but that left an action list, not robot traffic, and it stopped before the
last two steps. The first two actions match `NativePutDownActions.cs:13-16` (range, speed, trigger). **Decision: ask for action-level
scripts only, with values, and do not ask it for wire traces.** Wire traffic belongs to the action layers already covered.

## ChatGPT check 1 (built code against the engine, 2026-10-31)

ChatGPT compared PutDownBlock's C# with the engine. The paste was cut off in its secondary-failure row, so it is not saved as a report.
About 30 rows came back SAME or UNKNOWN, with none DIFFERENT. Spot checks held:
- **Reaction lock:** DisableReactionsWithLock skips a trigger whose table flag is 0 (0x005A283E..0x005A2846), so the empty set in
  `ReactiveBehavior.cs` is right. This closes the question left open above.
- **RNG:** IBehavior::GetRNG reaches [Robot+0]+0x14, the context RNG, and RandDblInRange is (b - a) x u + a (0x0082FA56..0x0082FA66).
- **Open:** whether `ReleaseCarriedObject(forget:false)` equals SetCarriedObjectAsUnattached(false).

It read the C# from the public GitHub repo, not an upload.

## Checked and matching

- **Item 1:** vtable slots, operator bodies, UpdateAllBlockConfigs order.
- **Item 2:** rotation gate (30 and 10 degrees; mode 5 or 0), diagnostic does not stop the broadcast.
- **Item 3:** chunking, gates at 0x0064F9C4 and 0x00535A9C.
- **Item 5:** QueueNow deletes the head before insertion.
- **Item 6:** body and PostAudioEvent's enabled test.
- **Not re-read:** rows 1.10..1.16 beyond labels, 2.02..2.03, 3.03..3.04 literals, 3.13, 4.02..4.07 (AbortAll agrees with its calls by
  name only).

## Still open

- `<u8>` passes cvtColor code 4 (0x0064F8D0). On one-channel input stock OpenCV would assert. Read the callers before relying on this path.
- ConfigurationType names; the event 72 tag; the StartActing closure census (M8-024); M15-069's other sites.

## Method note

Its literal and label habits are the remaining gap: it should decode every constructor argument and look up names it can infer. The
report had no access to CLAD, so labels of payload fields should be expected to come from Claude's check.
