# M11-050-MARKER-KEY-CARRIER-1

## Claims and evidence

- The stock inner Camera is created with ID 0 at 0x006B0016 and 0x0085DD20–0x0085DD24. The detector stores that bound camera at 0x008750A4 and passes it into the observed-marker list at 0x0087554C–0x0087555C. The marker camera copy includes ID separately from calibration at 0x0085DE10–0x0085DE1E. CLAHE rolling-shutter reconstruction forwards the same camera at 0x006B495A–0x006B4988. The managed worker captures ID 0 with each original detector result before crossing the mailbox. The public ObservedMarker shape and raw result list stay separate from that carrier.
- The component's outer Camera is constructed from the robot ID at 0x006500DA–0x006500E0, with the actual robot constructor value at 0x0050FC38. Stock attachment supplies 1 at 0x004ED07A–0x004ED07C. The managed recipient compares with the captured owner's NativeRobotId. Shared calibration does not make the numeric IDs equal. GetHistoricalCamera copies the outer camera and then assigns image-time pose at 0x00513A66–0x00513A8A.
- The native marker call requests the actual key in ComputeAndInsertStateAt at 0x00654D88–0x00654D92. The managed overload returns the same key from that computation under the existing history gate. Timestamp mismatch at 0x00654F0C drops first. Camera mismatch at 0x00654F10–0x00654F18 bypasses key validation. Equal camera IDs call membership at 0x00654F24 using the exact key, including zero, against the original history's reverse index at 0x0053214C–0x00532186.
- An absent equal-ID key warns VisionComponent.Update.InvalidHistStateKey with signed key=%d at 0x00654FEC, then drops only that marker. Accepted observations retain source order and use the historical outer camera at 0x00654F2C–0x00654F88. The existing BlockWorld and Docking recipients consume the filtered list, and the raw frame result keeps the detector list. The original owner and frame fences still apply.

## Files and tests

- Vision/NativeRobotStateHistory.cs returns the key alongside the existing history result. Vision/VisionSystem.cs carries Camera ID in the internal result and applies the ordered key gate. PickupPyramidNativeExecutionTests drives real rendered F2 through the stock worker and tests the defined equal-ID recipient via an explicit fixture. RobotStateHistoryAdmissionTests checks the wrapped key zero and reverse membership.
- The manager ran original ARM marker-loop fragment 0x00654F0A–0x00654F2A with source-shaped operands. Timestamp mismatch short-circuits, ID 0 against 1 bypasses even an invalid key, same-ID false drops, and same-ID true accepts key zero. The fixture controlled the membership return. Existing history tests reach the managed actual reverse index. Builder passed 127/127 affected and 6/6 final focused. Manager independently passed 120/120, including unchanged marker, Acapela and OKAO oracles.

## Settlement recommendation and MISSING

- M11-050 is proposed for settlement for the supported production marker path. The historical marker and computed-key handoffs establish the earlier gates, camera construction and index. This job closes the marker Camera ID and same-ID key recipient gap. No fidelity status or approval changed.
- An alternate shipped camera producer into this FIFO was not established. The equal-ID tests exercise the defined recipient with an explicit fixture, and do not claim a new producer. Broader history, marker geometry, calibration, mailbox and docking behavior remain in their own records. No hardware run was made.

## Workers

- Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults.
