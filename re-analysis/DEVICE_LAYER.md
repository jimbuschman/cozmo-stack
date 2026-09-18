# M3 — `Cozmo.Robot` device layer

Status: **implemented, unit- and capture-verified; hardware acceptance pending** (2026-09-18)

This is the first layer above the frozen M1 transport and M2 protocol baseline. It turns the verified wire
messages into three stateful pipelines plus a live view of robot state, as ordinary library components.
Nothing here depends on the Android app, `libcozmoEngine.so`, or Python at runtime.

```
Cozmo.Robot
  CozmoRobot          connect, handshake, routing, camera start/stop, simple commands
    .State            RobotStateTracker   identity, firmware, telemetry, calibration
    .Camera           CozmoCamera         chunk reassembly -> CameraFrame (decodable JPEG)
    .Display          CozmoDisplay        FaceBitmap -> RLE payload -> animFaceImage
    .Audio            CozmoAudio          PCM -> mu-law -> 744-sample frames, paced
```

## 1. Camera

`ImageChunk` (0xF2) carries a fragment of a "minimized" JPEG; `ImageImuData` (0xF4) carries the gyro rates
sampled with the frame. `CozmoCamera` reassembles by image id, and rebuilds a standalone JPEG.

What the robot actually sends, confirmed against the 2026-09-18 firmware-2457 capture:

| Observation | Evidence |
| --- | --- |
| Chunk ids run 0..n-1 in order, one image at a time | 28 complete images in `captures/` |
| `imageChunkCount` is non-zero only on the final chunk | same |
| Encoding 8 (`JPEGMinimizedGray`), resolution 4 (QVGA) | same |
| Chunk count varies 4..7 as auto-exposure settles; each chunk ~1 kB | same |
| Payload byte 0 is a colour flag, not entropy data | PyCozmo `client.py::_process_completed_image`, and it is 0 on every grayscale frame captured |

Reconstruction: prepend the fixed JFIF + quantisation + Huffman header the encoder assumed, patch height at
offset 0x5E and width at 0x60, drop payload byte 0, strip trailing 0xFF padding, re-insert the 0x00 that JPEG
requires after every 0xFF, append end-of-image. A colour-flagged frame uses the three-component header and is
encoded at half the resolution's width, so it has to be stretched horizontally on display.

The header bytes and the size offsets match the engine's own `MiniGrayToJpeg` / `MiniColorToJpeg` as
transcribed in PyCozmo `camera.py`; the offsets were re-derived independently from our own reconstruction.

A JPEG entropy stream re-synchronises after a one-byte shift, so decoding alone cannot prove where the stream
starts. The colour-flag reading is what settles it, and it is checked against the capture rather than assumed.

## 2. Display

`animFaceImage` (0x97) carries a run-length payload for the 128x32 one-bit face. Commands are a 2-bit opcode
and a 6-bit operand:

| Opcode | Meaning |
| --- | --- |
| 00 | skip n+1 whole columns |
| 01 | repeat the previous column n+1 times |
| 10 | run of (n>>2)+1 pixels down the column; either low bit set means draw |
| 11 | the same, plus 16, so runs of 17..32 |

The decoder is a faithful port of the state machine PyCozmo derived from the robot, including the implicit
column advance when a column's runs reach 32 rows. It is verified against **28 image/byte-sequence pairs
captured from Cozmo itself** (`tests/.../Fixtures/face_images.json`, exported from PyCozmo's fixtures): every
one of Anki's own sequences decodes to the expected picture.

The encoder deliberately uses only the two run commands, emitting exactly 32 rows per column. The
skip-column and repeat-column commands interact with the decoder's "last draw" and "repeat shift" state in a
position-dependent way that PyCozmo's own encoder flags as not fully understood, and the saving is
irrelevant: a blank face is 128 bytes instead of 2, against a 1420-byte message limit. In exchange the
encoder is exact for every possible image, which the round-trip tests check.

Two pathological images (per-pixel noise, alternating rows) need a command per row and cannot fit in one
message. `CozmoDisplay` rejects those with a clear error instead of sending a truncated face.

## 3. Audio

`animAudioSample` (0x8E) carries exactly 744 8-bit samples, `animAudioSilence` (0x8F) is an empty frame, and
`setAudioVolume` (0x64) takes a u16 level. 744 samples per animation tick at about 30 Hz is 22.05 kHz, which
matches the sample rate of the app's own voice assets.

The codec is G.711 mu-law. This is still **hypothesis-level**: PyCozmo asserts it and the frame arithmetic
agrees, but we have not disassembled the robot-side consumer, and no capture contains robot-bound audio. The
hardware tone test is what will confirm it: a correct codec gives a clean steady note, a wrong one gives
noise at the right duration.

`CozmoAudio.Play` paces frames at the frame interval so the robot's buffer is not overrun.

Two encoder bugs were found and fixed while writing the tests: clipping was applied before the bias was
added, so full-scale samples wrapped to silence, and negating `short.MinValue` overflowed.

## 4. Tests

119 tests pass (`dotnet test`), of which the M3 additions are:

* **mu-law** — reference endpoints, idempotence over all 256 codes (0x7F is mu-law's second zero), round-trip
  error inside the quantisation step, frame splitting and silence padding, tone length/fade/frequency.
* **display** — round trip over 9 synthetic images and all 28 fixture images, the 28 Cozmo-produced byte
  sequences decoding to the expected pictures, uniform-image encoding shape, oversize rejection, message
  identity and pacing.
* **camera** — synthetic reassembly, gyro pairing, supersede and missing-chunk handling, header patching and
  byte-stuffing, pass-through of already-complete encodings, colour-flag geometry.
* **capture replay** — the real image chunks from `hw_fw2457_probe.log`, replayed through the actual receive
  path so the robot's resends are dropped, reassembled and then **fully Huffman-decoded**: every frame must
  come out as a 320x240 single-component baseline JPEG of exactly 1200 MCUs ending exactly where the stream
  ends. A baseline JPEG decoder lives in the test project for this.
* **robot state** — identity, firmware, calibration and telemetry tracked correctly across a 20 s capture.

## 5. Hardware acceptance

Three commands, to be run against a real robot on its own Wi-Fi:

```
cozmo-conformance camera 172.31.1.1 --count 10 --out shots
cozmo-conformance face   172.31.1.1 --pattern test --seconds 8
cozmo-conformance tone   172.31.1.1 --hz 440 --seconds 2 --save tone.wav
```

Pass criteria:

1. **Camera** — the saved `.jpg` files open in any viewer and show the room from Cozmo's point of view.
2. **Display** — the pattern on the robot's face matches the ASCII art the command prints.
3. **Audio** — a clean, steady 440 Hz tone with no clicks or stutter, lasting two seconds.

Each command writes a full frame log and prints its absolute path.

## 6. Known gaps

* Colour streaming is implemented from the flag semantics but has never been exercised; the capture is
  grayscale only.
* `ImageRequest.ImageResolution` is sent as 0 (what the probe used); the robot chose QVGA regardless. Which
  resolutions it honours is untested.
* The procedural face (the 19-parameter eye model) is not implemented; this layer takes bitmaps.
* `setAudioVolume` range is unknown. Nothing here writes it by default.
* Frame timestamps arrive as 0 on firmware 2457, so `CameraFrame.Timestamp` is not usable for pairing.
