# MEET-COZMO-ADMISSION-1

## Stock activity and chooser

- The existing host activity request delivers the high-level value 3 in place of the shipped app's 0x92 input (0x738A94, 0x74D90A, 0x5A147E–0x5A14C8). The stock activities_config.json supplies MeetCozmo as public activity 6, BehaviorsOnly with Simple strategy and no interlude. The checked class uses the no-op activity Update at 0x5A7372 and the base chooser at 0x5B387C–0x5B3A6C.
- On a new request, the old activity deselects and the manager stops the ordinary run or preserves the current reaction, then clears resume. It selects the actual Meet activity, reports activity 6, stamps the parent clock, pauses Freeplay for GameControl and chooses immediately when ordinary (0x5A106C–0x5A1230, 0x5B3154). Duplicate activity 3 leaves the existing run and parent clock in place. The adapter clears both spark IDs on either path (0x5A4FA2–0x5A4FE0). An explicit activity replacement follows the same owned stop and deselection path.
- The chooser traverses stock configured behavior IDs in native order (0x60A3EE, 0x60A596). A nonpositive score consumes no draw, a positive nonrunning score uses the original shared RNG draw and f64 addition followed by f32 rounding, and strict comparison retains an earlier equal score (0x60A44A–0x60A4F6). DriveOffCharger and PutDownBlock retain their original configured owners. Pristine EnrollFace and RespondToRenameFace have no staged name and score zero (0x5FCB08–0x5FCB26, 0x5FCD00, 0x6006A8–0x6006C0, 0x60082E).
- A distinct MeetCozmo_FindFaces_Socialize owner uses its own shipped JSON, including the 10-second face age, wider early body sweeps and additional head changes. The inherited native scan starts with the 32 mm lift action and then the scan's look-up step (0x5E2684, 0x548B84 to 0x54899C, 0x5C1DC0–0x5C1DEA). HANDOFF-ORDINARY-FREEPLAY-1.md covers the reused scan phases and retained actions.
- The constructor-pristine Interact candidate uses the common gates before its own current-origin face query since image timestamp zero (0x5BEFA2–0x5BEFCE, 0x5C1EE8–0x5C1F04, 0x5C23AC–0x5C23FC, 0x4F55B8, 0x4F5A10, 0x56C110–0x56C27E). A positive checked candidate consumes its draw even if it loses. If it wins, the previous ordinary behavior is retired at the native switch prefix (0x5A2A74–0x5A2AB4, 0x5A1E6A). Its Init at 0x5A1E94 is a visible MISSING. The stack holds at that boundary without substituting a runner-up or repeating draws for a behavior it cannot execute.

## Files and checks

- NativeBehaviorRuntime.cs selects and retires the configured activity and routes the explicit unbuilt-winner boundary. Activities.cs preserves the stock chooser and handles constructor-pristine partial scores without creating a dummy action owner. BehaviorContainer.cs and NativeFindFacesSocialize.cs construct the separate Meet scanner from its JSON. FaceBehaviors.cs reuses the native current-origin selection with the Interact unnamed surcharge.
- MeetCozmoAdmissionTests.cs drives the public high-level request against shipped activity and behavior resources and a fake robot. It checks activity 6, the GameControl pause, a real scanner lift message, duplicate request and exit, configured DriveOff and PutDown winners, preservation of an active reaction, and a queued request against a replaced robot. It supplies a face through production camera ingress and observes the old scanner stopped before the MISSING Init message, with no runner-up or further chooser draws. It also checks the positive Interact losing draw while a real DriveOff continues after the charger-platform clear.
- The manager independently disassembled the selected byte, OnSelected, timer and GameControl sequence, and the native nonrunning draw.

## MISSING

- Interact Init and its verified name, look, drive and tracking children remain open (0x5C2154–0x5C220C, 0x5C2258–0x5C22AC, 0x5C24B4, 0x5C2518, 0x5C2704). When scoring inputs are unavailable, the activity visibly stops its checked ordinary execution and holds at the gap. That host guard claims no native winner for unknown input.
- No stock named request, scan cancellation, rename delivery, or durable named enrollment/report was added (0x5FF744–0x5FF890, 0x5FE2F4–0x5FE528). Full physical scan, charger and placement motion and original app UI flow were not exercised. No fidelity status or approval changed.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.

Final affected tests passed 239/239. Independent manager affected run passed 223/223 with zero skips. Independent source review found no remaining blocker within this scope. Batch full gate pending.
