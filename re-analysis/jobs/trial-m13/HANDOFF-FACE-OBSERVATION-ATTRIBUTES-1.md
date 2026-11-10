# FACE-OBSERVATION-ATTRIBUTES-1

## Claims and evidence

- VisionSystem's four persistent tracker flags are set through 0x006B1A2A, 0x006B1B5C, 0x006B1BAC and 0x006B1BFC. Stock constructor defaults are clear at 0x0086B2BE–0x0086B2C2. The stack captures enabled flags at detector execution and carries the result into the prepared frame. DetectFaces marks processed mode 2 at 0x006B5282–0x006B528E. The four attribute enables do not add their own processed-image mode bits.
- FaceTracker creates the EX, SM and GB handle/result pairs at 0x0086BDF2–0x0086BE36. The host allocates them on the first optional request within the existing detector session to preserve the default allocation path. Healthy teardown follows native SM, GB, EX order at 0x0086C5AE–0x0086C79A. An emulator fault retains existing detector retirement and does not synthesize a complete report.
- Attribute work runs after successful part-point extraction. The native caller skips all optional calls when DetectFaceParts returns false at 0x0086DA14–0x0086DA16. The normal PT no-parts status leaves zero/default attributes. A late face-direction failure at 0x0086CF32–0x0086CF72 still succeeds after the point lists were written and uses the default zero roll from 0x0087DBB2–0x0087DBC6. The attributes still run and do not change enrollment inputs (0x0086DB7E).
- The managed session calls original ARM wrappers in expression, smile, gaze/blink order at 0x0086D1E8, 0x0086D3C8 and 0x0086D550. Expression order [0,1,2,3,4] comes from 0x0086D3B8. The byte conversion at 0x0087E0B8–0x0087E0CA and strict maximum at 0x0087E08C–0x0087E0B6 retain the first tie and neutral all-zero output. Smile scales amount and confidence at 0x0086D474–0x0086D4A0. Gaze uses signed outputs unscaled at 0x0086D604–0x0086D61E. Blink scales both outputs by 0.001 at 0x0086D658–0x0086D680.
- Ordinary guest status preserves source defaults or already written fields. Expression estimate -8 skips its result at 0x0086D254–0x0086D296. Smile failure leaves its own invalid pair. Shared gaze/blink setup failure leaves both invalid. Gaze-getter failure skips blink at 0x0086D602, while blink failure retains valid gaze. The immutable stock attributes supply the existing 0x46 CLAD fields at 0x00713604–0x0071364C. Unchecked detector, name or pose carriers still use the prior local-only boundary.

## Files and tests

- Emulation/OkaoFaceDetection.cs and Emulation/OkaoFaceAttributes.cs manage original guest calls and captured fields. Vision/Faces.cs and Vision/NativeFaceAttributes.cs carry the detector result. Vision/VisionSystem.cs and RobotObservedFaceReport.cs bind the existing wire and typed API.
- NativeFaceAttributesTests feeds the existing canonical synthetic frames into the managed guest. Independent Unicorn calls to the original wrappers supply expected scores and float bits for all-enabled, gaze-only and blink-only modes, a PT no-parts frame, and a native smile failure with gaze/blink still valid. FaceIdentityTests reaches the real recognized camera path and checks typed/raw 0x46, detector-time mode capture across a later toggle, and no extra 0x43 mode bits. Local expression projection stays as before when expression is disabled. Affected 139/139 and final independent 21/21 passed, including unchanged NativeOkaoOracleTests. Forced expression/gaze/blink errors and late PT direction failure were source-checked without dedicated regressions.

## MISSING

- Wider tracker and recognizer cadence, unproved nonstock carriers, and other incomplete parts/pose cases retain their existing gaps. The source's eager optional handle allocation timing is not reproduced for disabled host sessions.

## Workers

- Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager ran the original three ARM wrappers under Python Unicorn using the existing canonical synthetic image sequence and compared signed words and float bits. Review checked the disabled local projection, immutable typed fields and detector-time capture. Builder raised the normal guest failure test boundary, resolved for the canonical smile failure.

## Records

- M14-010 owns the optional detector calls and M14-008 owns the report. M14-001 and M14-011 own adjacent tracking/recognition. No whole record settlement is proposed.
