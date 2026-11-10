# STOCK-PET-DETECTOR-1

## Claims and evidence

- VisionSystem calls PetTracker Init at 0x006B1018–0x006B1026 before applying initial modes and schedules. PetTracker clears readiness at 0x0087BE94, reads and logs the guest version at 0x0087BEA0–0x0087BF26, and reads the shipped PetTracker config in source order at 0x0087BF52–0x0087C040. The production vision stage uses the pinned libcozmoEngine.so and libc++_shared.so, retaining the existing host-only configuration path for unavailable libraries and supplied fixtures.
- PetTracker creates the detector in mode 5 and the result handle at 0x0087C066–0x0087C07A. Setters run in source order at 0x0087C09C, 0x0087C2D4, 0x0087C386, 0x0087C414, 0x0087C4B6 and 0x0087C556. The shipped values are max 4 pets, angle selector 0 and mask 0x803803, search cycles 4/8, face size 60/240, threshold 900, lost 2/2 and steadiness 15/15. Native nonzero results stop Init with the source error and readiness result at 0x0087C0BC–0x0087C56E and 0x006B1034–0x006B1086. Success marks ready at 0x0087C5C4.
- The source retains the same guest handle pair across frames and deletes result before detector at 0x0087C7F6–0x0087C85C. The stack fences the session to its original vision worker and retires it on replacement or disposal. Public detector access is serialized with the guest operation and availability reflects a live stock session. Defined delete failures log at 0x0087C812 and 0x0087C86E and set _errG at 0x0087C848 and 0x0087C8A4. A supplied detector override keeps its existing route.
- OMCV receives the base grayscale image after prior detection rectangles are masked at 0x006B3940–0x006B39D6. PetTracker calls Detect with its fifth stack argument at 0x0087C9AE, count with mask 0x180000 at 0x0087CA2A, then each result at 0x0087CAA8. The result ABI writes 56 bytes at 0x00935204–0x009352A4. At 0x0087CAB2–0x0087CB86 it reads signed ID and score, maps cat/dog masks, subtracts signed truncating size halves from the center, converts rectangle components to f32, and sets the tracked flag only when the raw field equals 1. The frame timestamp is attached to each result. A successful empty result still sets mode 9 at 0x006B5326 and reaches the existing native pet map and 0x48 producer.
- Defined Detect, Count or Info failures warn at 0x0087C9C4, 0x0087CA3E or 0x0087CBD6. VisionSystem warns at 0x006B39EE, logs an error at 0x006B52D6, sets _errG at 0x006B5310 and exits without a mailbox result at 0x006B5692. A host guest fault instead retires that guest and reports MISSING. Failed or stale sessions cannot commit empty observations or affect a replacement.
- The guest uses its own process CPU clock import at 0x00935BF8 and 0x008FF490. The stock constructor leaves the optional elapsed-time cutoff at zero at 0x00934A0C–0x00934A30. The guest checks that value before calling the elapsed-time helper at 0x00935974–0x00935994 and 0x009373E4–0x009373EC. The emulator retains its separate execution deadline.

## Files and tests

- Emulation/OkaoPetDetection.cs owns the pinned guest, configuration calls, result ABI and healthy cleanup. Vision/NativeVisionInitialization.cs and Vision/VisionImageQuality.cs bind the real Pet Init stage and diagnostics. Vision/Faces.cs serializes guest access. Vision/VisionSystem.cs joins the original stock session to the worker, fences queued results, and preserves the existing pet report and image-quality paths.
- StockPetDetectorTests checks the independent 17-frame raw guest corpus under constant and advancing clocks, actual threshold setter failure, stock F2 camera input through the normal robot tick and raw/local 0x48, mode omission, defined failure suppression and same-owner guest reinitialization. The injected Detect return status checks that an actual worker invocation emits the three error keys, sets _errG, and preserves the prior PetWorld. The reinitialization test fences a prepared old result and confirms the fresh guest starts its IDs again. Separate fake robot tests replace the owner, dispose VisionSystem, and retire a session while an external Detect is in flight.
- The raw fixture is the CC0 Chelsea image by Stefan van der Walt. Source is scikit-image v0.25.2, converted to grayscale, bilinear resized to 120×80, and centered in 320×240 gray130. The decompressed fixture SHA-256 is 96ee87b0d92b40763309d763da3c2840f626ced67199047cbed724d915243e1d. [Fixture provenance](https://scikit-image.org/docs/stable/api/skimage.data.html#skimage.data.chelsea)
- The manager ran original ARM with the pinned two libraries, exact setter order and 17-frame fixture. Both zero and advancing CPU-clock inputs gave identical 25 count/info rows. The first cat has ID 1, score 965, center (164,116), size 96×96 and flag 0x100. Later tracked records carry score -1 and flag 1. The corpus also covers empty results and reacquisition as ID 2.

- The builder passed 134/134 affected cases before the final initialization and teardown corrections, then 49/49 in the final combined rerun. An interim combined run hit a fixture startup NullReferenceException that did not reproduce in isolation. The two pet fixture classes now use the existing nonparallel collection. The precise competing cause was not established. The manager independently passed 56/56, including the unchanged NativeOkao and Acapela TTS oracles.

## MISSING

- Host packages without the two pinned libraries retain the existing vision-only fallback and stock pet detection is unavailable. Dog positives, a physical reconnect, native Count/Info failure injection, and non-stock frame shapes or detector tuning are outside the demonstrated corpus. No hardware run was made. Existing human OKAO and Acapela oracle paths are untouched.

## Workers

- Reader used gpt-6.1-sol at medium effort. Builder and reviewer used the defaults. Review checked guest ABI, owner and session fences, retirement, concurrency and failure handling.

## Records

- M11-016 owns stock detection, M11-059 owns Init and readiness, M11-080 owns metering inputs, and M14-014 owns the observation report. No fidelity status or approval changed.
