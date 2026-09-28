# I-M14 gap pass 1 — detector outputs and face-album write tail

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28. Read-only investigation of `libcozmoEngine.so`; Ghidra was navigation only and every result below was checked in the binary with Capstone.

| step | what the original does | citation | record | class |
| --- | --- | --- | --- | --- |
| G1-1 | On successful `OKAO_SM_GetResult`, sets TrackedFace smile-valid (+0xB8) to 1, converts the two signed integer outputs to float, scales the value stored at +0xBC by 0.01 and the value at +0xC0 by 0.001. These are output scale factors, not thresholds. | 0x0086D430 GetResult; 0x0086D474..0x0086D4A0 conversion, validity store, multiplications and stores; literals 0x3C23D70A and 0x3A83126F | NEW detector record | EXACT_SOURCE |
| G1-2 | Gaze and blink are individually enabled by Impl bytes +0x43 and +0x44. Successful gaze output sets +0xC4 and stores the two converted signed integers, unscaled, at +0xC8/+0xCC. Successful eye-close output sets +0xD0 and scales both signed results by 0.001 into +0xD4/+0xD8. | 0x0086D5D0..0x0086D622; 0x0086D622..0x0086D680 | NEW detector record | EXACT_SOURCE |
| G1-3 | Expression gets five signed integer results and, on success, converts each to float and passes it with the five-entry type table to the TrackedFace expression setter. No 0.001/0.01 gate occurs here. | 0x0086D298..0x0086D2FC | NEW detector record | EXACT_SOURCE |
| G1-4 | SaveFaceAlbum obtains two serialized byte vectors under the vision mutex, checks their sizes against NV tags 0x184000 (album) and 0x183000 (enrollment), rounds both vector sizes to a four-byte boundary, then writes album first and enrollment second. It stops before the second write if the first enqueue fails. Empty data uses the corresponding Erase path instead. | 0x00657180..0x0065720E; 0x006573A0..0x006573C0 writes tag 0x184000; 0x006573C2..0x006573EA writes tag 0x183000; 0x0065745A..0x00657474 erases 0x184000 on the empty path (the paired 0x183000 erase follows) | NEW album record | EXACT_SOURCE |

Corrections to X2: F18 and F19's “threshold” wording is rejected and replaced by G1-1/G1-2. F26's untranscribed write tail is closed by G1-4.

Still open for the next pass: `FaceRecognizer::RecognizeFace` (0x008640E4), `MergeFaces` (0x008638AC), and the enrollment-entry helpers they call.
