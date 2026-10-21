# Drive-off animation gate review — partial (2026-10-21)

**Codex review; Claude spot-check pending**

Examined HEAD19b0a313 and report `20261021-m13-driveoff-remaining-joins.md`.
Independent native check of the gate/override ownership only; not the report's
full action/cancel/constructor path or a builder release. No code/tests changed.

**Verified evidence mismatch:** current M13-017 is IMPLEMENTATION_GAP, titled
“BehaviorDriveOffCharger: runnable on the charger, the drive distance and the
leaving-the-contacts update”. Its evidence says “pushes driving animations when
the animation state is 3 (0x005C0B4A)”. The native pointer chain is different:

- Init0x005C0B34/38/3A reads robot+264 -> AI+30 -> component+14, compares3 at3C.
- AI ctor0x00569B3A resolves to SevereNeedsComponent ctor, then stores that
  pointer at AI+30 at0x00569B3E. Component ctor0x005729FC..0x00572A14 writes3
  to+14 at0x00572A0E. Thus this is SevereNeeds state, not handler animation state.
- When equal3,0x005C0B40..4A calls the actual driving handler at robot+24C
  with behavior name+40 and a ROM triplet. Raw words atC6731C are97,93,8F hex.
- Init clears behavior+120 at0x005C0B30 and sets it only after Push at0x005C0B50.
  StopInternal0x005C0D90..DA4 tests that flag; nonzero forwards the name and
  handler through8CABCC, zero returns. It does not clear the flag here.

Current ChargerBehaviors.OnStart retains a visible MISSING for this caller;
there is no new implementation silently using the wrong gate. This blocks
building from the old evidence sentence, not acceptance of M1-M5. Handler
M5-063 remains its already accepted boundary. The complete removal veneer and
IBehavior Stop/cancel ordering are reused only as report leads here, not newly
certified by this partial pass.

Next checkpoint: correct M13-017's frozen gate evidence/inventory with the
actual owner chain, integrate the reviewed animation producer/ownership rows,
and follow the applicable approval process before a builder release. Full
SevereNeeds Set/Clear writer behavior is still pending research; constructor3
is not proof that the field stays3. New request R-M15-SEVERE-INPUT scopes that
producer. No default3 adapter or host-posted B5/B6 shortcut is authorized.

Remaining report sections (action-tag/completion/cancel, constructor defaults,
wait/re-entry, full pop lifetime) need independent checking. No whole-record
settlement, new fidelity classification, hardware test or acceptance.


## Expanded manager review (2026-10-21, baseline b08efa68; B-M13-10 7f42b427)

**Codex review; Claude spot-check pending**

The manager re-read the executable instructions for Init 0x005C0B18..0x005C0BA6, Transition 0x005C0BB8..0x005C0C84, StopInternal 0x005C0D90..0x005C0DA4, Update 0x005C0DA8..0x005C0E32, Stop ordering 0x005BD10C..0x005BD126, StopActing 0x005BD34C..0x005BD43A, two-argument constructor 0x005470F0..0x0054725E, and completion receiver 0x005BE1E6..0x005BE224. Calls were resolved through the .so PLT relocation names, not copied from the report. This adopts the bounded caller/constructor findings below; it does not adopt every address quoted in the report's historical manifest snapshots as newly verified.

- D1-D5 supported. AI constructor call/store 0x00569B3A/3E identifies SevereNeedsComponent, whose constructor 0x005729FC..0x00572A14 stores 3 at +0x14. The ROM address loaded at 0x005C0B40/48 resolves to 0x00C6731C, whose three raw words are 0x97,0x93,0x8F. The removal veneer at 0x008CABCC switches to ARM; 0x008CABD0/D4 and literal 0x008CABD8 resolve to PLT 0x004A5818, RemoveDrivingAnimations. No inferred handler-state gate.
- D6 supported, with the intermediate last-run-clock store retained: StopInternal runs before the timer store and StopActing(false,false). StopInternal does not clear +0x120. Existing C# DriveOffCharger has no pop override, so inherited ActionBehavior cancellation cannot establish that order.
- Wait/update matrix supported. Platform true/off-treads always calls StopActing(false,false), including when no action is owned. Platform true/on-treads re-drives only if +0x84 is zero. Platform false/nonzero tag waits; platform false/zero tag stamps whiteboard and completes without testing off-treads. C# branches exist, but its host handle is not the native action tag.
- StopActing clears +0x84 before ActionList.Cancel when keepAction=false; its post-call clear is conditional on the tag still matching. Current ChargerBehaviors calls CancelAction before StopActing; DriveStraight.RunAsync registers cancellation against the actual runner Tag, while StartActing() creates a different host handle. This is a concrete ordering/ownership gap, not an absent DriveStraight queue.
- Constructor evidence supported: 0x00547158 writes accel bits0x43480000/decel0x43FA0000, +0x88/+0x89 zero, +0x8A one. Literal words at 0x00547268/26C are 0xC2A00000 (-80) and 0x42C80000 (100); ordered nonnegative distance selects the latter. The positive default no longer needs new research. Manual-profile distinction and the full Init/Check path are not certified by this constructor review.
- Completion receiver 0x005BE1EC..0x005BE224 matches tag, clears it, invokes slot0x80(false), then tests running and callable before invocation. Current concrete StartActing overload explicitly reports its unbound constructor subscription; HandleActionComplete alone is not a live subscription.
- Reopened drive completion 0x005C0E9C..0x005C0EE2: success alone calls BehaviorObjectiveAchieved(4,true), then MoodManager current-time getter and TriggerEmotionEvent. Current OnDriveComplete only logs/reports missing objective behavior and calls the host Mood adapter. These remain downstream state/API gaps, not harmless diagnostics. Their complete callees are not newly extracted or adopted here.

### Current record comparison

M13-017 remains IMPLEMENTATION_GAP, titled "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update". Its current evidence still says "pushes driving animations when the animation state is 3 (0x005C0B4A)"; that sentence is contradicted by the owner chain above. B-M13-10 already corrected the stale contact-observation unresolved paragraph; it did not settle the action or severe-need inputs. M8-015 remains IMPLEMENTATION_GAP; its lifecycle evidence includes 0x0053E6A8..0x0053E6E0 and the checked StartActing/Cancel bodies, but the real behavior receiver join is still explicitly unbound in code.

### Open questions / release boundary

No new research queued. Before a complete drive-off build can be accepted: correct/freeze the gate inventory; establish the live SevereNeeds writer (existing held row39, not reviewed ahead of its allowed order); bind the concrete tag and constructor completion subscription with reviewed M8 ownership; account for objective/mood recipients. The report's recommendation to reuse existing queue and handler is supported at the caller boundary, but it is not permission to replace missing inputs with constant3 or to certify the shared owners. A new build can implement reviewed caller rows only with these remaining joins explicit. No record settled, hardware written, or whole-path PASS asserted.
