# AI-BODY-OBSERVED-PET-REPORT-1

## Claims and evidence

- VisionComponent consumes mode 9 at 0x0065457E through UpdatePets at 0x00655360–0x00655366. PetTracker::Update reads signed ID and score at 0x0087CAB2–0x0087CB56, maps the two result type masks at 0x0087CAD0–0x0087CAE4, and stores the image timestamp and result flag at 0x0087CB4A–0x0087CB6E. The opt-in supplied detector carries the converted rectangle and explicit flag. The vision worker snapshots the result and only the queued original-owner path consumes it as native. An authoritative empty result and an omitted or incomplete result have different state effects.
- PetWorld::Update at 0x0050B7F4 reads the prior map for each candidate. An existing ID bypasses rotation and increments the old 32-bit count at 0x0050B838–0x0050B8AE. A new ID uses the flag to select five or zero original-owner IMU samples with body threshold 0x3F060A92 and head threshold 0x3E32B8C2 at 0x0050B8C8–0x0050B8F4. The first admitted entry for a signed ID wins at 0x0050B8C2/0x0050B93A. The full replacement map becomes current at 0x0050B96C–0x0050B99E and reports traverse signed ascending IDs at 0x0050B9A2–0x0050B9A8 and 0x0050BA74–0x0050BA9A. The managed map belongs to the original robot and is replaced at owner changes. PetInitialDetection reads that map and uses a signed count greater than two at 0x00611B3E–0x00611B6A and 0x00611BB8–0x00611BBC.
- Tracker flag clear gates telemetry at 0x0050B9AC. A count other than one triggers the VERIFY at 0x0050B9C6, using literals 0x0050BB0C/0x0050BB40/0x0050BB68. The original helper at 0x0080D9A4 logs the error, sets _errG and forces a DAS flush at 0x0080D9F4–0x0080DA08. It still emits robot.vision.detected_pet at 0x0050BA04. The signed ID is the message text at 0x0050BB94 and the enum name is the $data field at 0x0050BB8C, 0x0079BAE8 and 0x01034300.
- PetWorld constructs and broadcasts RobotObservedPet at 0x0050BA2A–0x0050BA68. The 0x48 tag and original Pack at 0x00713BE0–0x00713C44 give 33 little-endian bytes containing i32 ID, u32 timestamp, u32 count, f32 score, four f32 rectangle fields and u8 type. The original owner's external interface gates both recipients at 0x00511DE8. The managed report follows the app enqueue at 0x00662526 before local delivery at 0x00662552. Owner and map generation checks fence reentry. The host PetObserved compatibility event runs separately after native publications, with handler isolation under M1-034.

## Files and tests

- Vision/Faces.cs adds the supplied detector contract, native replacement map and separate compatibility callbacks. Vision/VisionSystem.cs captures worker results, checks rotation with original-owner IMU, binds map lifetime to the owner and delivers source telemetry and reports. RobotObservedPetReport.cs defines the typed event and 33-byte packing.
- NativePetReportTests sends firmware F2 images to an attached fake robot, waits for the worker and checks the normal engine tick drain before asserting local, typed and raw observations. It covers signed order, first duplicate, score conversion, repeated counts, authoritative empty results, unavailable or incomplete input, interface suppression and owner replacement. Separate tests reach the rotation-admission seam and host callback reentry. The tracker flag test verifies telemetry and the error flag. The first queued fixture checks isolation of a throwing compatibility subscriber.
- The manager ran the original ARM Pack and SafeMessageBuffer with two independent payloads, including a negative ID, maximum u32 count, large integer-to-f32 score and signed zero rectangle coordinate. Both returned the expected 33 bytes. The builder passed 145/145 affected tests and 6/6 final focused tests. The manager passed 43/43 independent tests.

## MISSING

- The stock OMCV pet detector and its failure/lost-track path remain open under M11-016. Legacy supplied profiles cannot establish a native report. Malformed opt-in results may supply host metering rectangles and processed-mode bookkeeping while native continuity and reports are withheld. Missing original-owner IMU prevents a native commit when a new ID requires it. Tests check the rotation decision seam and three-count map independently of a live rotating frame and full PetInitialDetection trigger. No hardware run was made.

## Workers

- Reader used gpt-6.1-sol at medium effort. Builder and reviewer used the defaults. Independent review found that host callbacks could interrupt native delivery and that a reset could mutate a published map. The final diff separates callbacks from native publication, isolates their failures, swaps immutable map generations, and fences their owner lifetime. The manager independently recovered telemetry literals and ran the ARM packer.

## Records

- M14-014 owns the observation report. M11-016 owns the detector and M11-080 the metering consumer. M1-034 and M3-055 own handler isolation and DAS flush. No status or approval changed.
