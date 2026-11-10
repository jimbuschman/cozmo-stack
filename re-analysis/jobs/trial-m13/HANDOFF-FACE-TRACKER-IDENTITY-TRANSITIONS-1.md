# FACE-TRACKER-IDENTITY-TRANSITIONS-1

## Claims and evidence

- Tracker Update calls GetRecognitionData at 0x0086DBC2 for the current raw DT track. GetRecognitionData copies face ID, previous face ID, current track and previous track at 0x00863EB0–0x00863EF4 before 0x00863F00 normalizes the source entry. Normalize copies current fields to their previous fields at 0x00860F0C–0x00860F14. A lookup without a mapping still uses the negative raw DT identity at 0x0086DD4E–0x0086DD52.
- If the copied face ID differs from its previous face ID, the first candidate at 0x0086DBC6–0x0086DC3C always appends. The old ID is the prior face ID, or the negated raw detection ID when the prior face ID is zero. Independently, the second candidate at 0x0086DC54–0x0086DD02 uses the negated copied current track and current face ID when the copied current/previous tracks differ and the face ID is nonzero. Only the last list entry is checked at 0x0086DCB0–0x0086DCC2. Either endpoint matching suppresses the second candidate.
- UpdateExistingAlbumEntry erases exactly the prior track key and stores current and previous tracking values before later guest work at 0x00865B74–0x00865B98. UpdateRecognitionData maps the submitted track at 0x0086357A–0x00863588. For two distinct named identities it warns at 0x00863528–0x0086352A and calls RemoveTrackingID at 0x00863554–0x0086355A. The old identity's two track fields clear and only that association is erased at 0x00863866–0x00863874. Both enrolled identities survive and the submitted track is associated with the new identity. This route does not create a positive-ID merge.
- The VisionComponent drains identity changes at 0x006551E8–0x00655202 before enrollment completion at 0x00655204–0x0065526C and observations at 0x0065526E–0x00655274. The existing 0x47 and 0x46 recipients deliver the new list entry in that order.

## Files and tests

- Vision/FaceRecognizer.cs carries the copied track pair and original stock map writer. Vision/Faces.cs applies the two-candidate sequence before the existing vision result consumer.
- The manager ran the original tracker fragment at 0x0086DBC6–0x0086DD12 with controlled recognition output and native list operations. It confirmed changed-track-only -9 to 17, silence on unchanged tracks, first and positive merge cases, and suppression on either endpoint of only the last item. An independent original DT sequence verified track 1 on the initial drawn face, then track 2 when that face returns after 20 black frames.
- The manager also ran original UpdateRecognitionData at 0x00863384 with two loaded named identities and a submitted track. It rebound the track, kept both identities, cleared the old tracks, and GetRecognitionData copied the pre-normalized fields. These are local ARM fixtures with no hardware.

- FaceIdentityTests drives actual F2 camera frames through the stock guest, sees the initial -1 to 7 association, loses the track across 20 black frames, then sees -2 to 7 before the positive 0x46. The next stable frame emits no duplicate change. FaceRecognizerTests reaches the stock update with controlled recognizer matches, checking exact old-key erasure, named replacement and last-only suppression. The builder passed 113/113 affected and 56/56 final focused. The manager independently passed 74/74, including the unchanged OKAO and Acapela oracles.

## MISSING

- Unnamed/direct replacement and incomplete merge branches remain open. The shared ARM emulator still serializes DT/PT and FR work. No broader recognition-score projection or raw oracle changes are claimed. The managed two-named conflict uses a controlled recognizer, independently checked at the native map writer. No hardware run was made.

## Records and workers

- M14-008, M14-010 and M14-011 retain their current statuses and approvals. Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults.
