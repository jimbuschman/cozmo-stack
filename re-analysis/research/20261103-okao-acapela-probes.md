# OKAO and Acapela under the emulator: feasibility probes (Claude extractor, 2026-11-03; filed by the manager)

**Verdict.** Both shipped libraries run unchanged under the Unicorn emulator, and no record changes yet:

- **OKAO** face detection, parts and recognition work. They were tested on synthetic faces only, because the bundles contain no
  real face.
- **Acapela** TTS works with the Ryan voice and the engine's own licence. It runs about 3.8 times slower than real time.

The operator's licence position (2026-11-03): personal use at home, not sold or distributed.

The scripts are in the session scratchpad `probes\`: okao_probe.py, synthface.py, tts_probe.py, tts_fast.py and scanf_impl.py.
They are not in git.

## OKAO (inside libcozmoEngine.so)

**Results.**

- **Robot frame:** camera-35659 gives 0 faces, which is correct.
- **Synthetic face, detection:** a face is detected from frame 0..2 on (video-mode tracking). Raw +0xC is 750..901, behaving like a
  confidence against the threshold of 500; that name is inferred. The face is held for 2 frames after it disappears.
- **Synthetic face, other steps:**
  - PT returns 12 points on the eyes and mouth, but all 12 PT confidences are 0, which is UNKNOWN.
  - GetFaceDirection gives (-5, 3, 0).
  - FR: ExtractHandle, RegisterData into an album (common, 1000, 4), then Identify on the same face gives id 0 with score 1000.

**Speed:**

| Step | Instructions | Wall time |
|---|---|---|
| DT detect, 320x240 | 2.2..5.9M | 0.06..0.16 s |
| PT | 4.5M | 0.09 s |
| FR ExtractHandle | 53.7M | 1.76 s |
| FR RegisterData | 7.9k | 3.5 ms |
| FR Identify | 47k | 4 ms |

OKAO called only malloc/free, mem* and the libgcc division helpers. There is no libm, no time call, no thread and no licence check.
Heap use is about 1.6 MB.

**How Anki initialises and calls it** (addresses checked in the .so unless marked otherwise):

- **Ctor:** 0x0086B2A8 (decomp only). It takes the FaceDetection config and creates RandomGenerator(seed 1) at +0x260, used to
  shuffle multiple detections.
- **Version:** OKAO_CO_GetVersion at 0x0086B4EA returns 6.0.
- **Common handle:** OKAO_CO_CreateHandle at 0x0086B598 stores to +0x60. It is malloc-backed (0x008FF04C..0x008FF05C), and
  Set[BW]MemoryArea is never called.
- **Mode:** "video" (default, and the shipped vision_config.json line 35). DT_CreateHandle(co, 5, 10) at 0x0086B96E;
  singleImage would be (co, 4, 10) at 0x0086BAE8.
- **Video settings:**
  - MV_SetDelayCount(1) (0x0086B984);
  - MV_SetSearchCycle(2, 2, 5) (0x0086BC40..48);
  - MV_SetDirectionMask(0) (0x0086BD0A..0E);
  - MV_SetPoseExtension(1, 1) (0x0086BEA2..A8);
  - MV_SetAccuracy(0) (0x0086BF6A..6E).
- **Detection parameters:**
  - DT_SetAngle(dt, 1, 0x803803) at 0x0086B77E..88;
  - DT_SetSizeRange(dt, 48, 640) at 0x0086B7F0/0x0086B7F4; optional minFaceSize/maxFaceSize keys are read at 0x0086B904;
  - DT_SetThreshold(dt, 500) at 0x0086B9F2, call at 0x0086BA7E; optional detectionThreshold key.
- **Result handles:** DT result +0x68; PT +0x6C with PT_SetConfMode(1); PT results +0x70/+0x74; EX +0x78/+0x7C; SM +0x80/+0x84;
  GB +0x88/+0x8C (0x0086BBBC..0x0086BE36).
- **FR init:** FaceRecognizer::Init(+0x138, common) at 0x00862A18 (decomp only; RECOVERABLE_GAP) makes two
  FR_CreateFeatureHandle calls and FR_CreateAlbumHandle(common, 1000, 4). It runs asynchronously in the shipped config
  (vision_config.json line 40).
- **Detect:** OKAO_DT_Detect_GRAY(dt, [img+0x14], cols [img+0x10], rows [img+0xC], 0, result) at
  0x0086D75E..0x0086D776.
- **Results:** GetResultCount at 0x0086D7FC; GetRawResultInfo at 0x0086D958. A +0x2C != 0x100 result goes to TrackedFace+0x18;
  the centre and size go to CenterToSquare with angle 0 (0x0086D96A..0x0086D998).
- **Parts:** PT_DetectPoint_GRAY at 0x0086CD18..28; a -8 return exits silently (0x0086CD2E). GetResult(ptres, 12, ...) at
  0x0086CD36..46, then GetFaceDirection at 0x0086CF20..32.
- **FR extract:** ExtractHandle_GRAY at 0x00865034 / 0x00865A1C (decomp only; RECOVERABLE_GAP).

## Acapela (libacattsandroid.so plus obb tts/Voices)

**The route.** The engine never calls the BABILE C API. It calls Java static methods on `com/anki/cozmo/CozmoTextToSpeech` through
JNI. That Java class is the app layer, so the stack must supply this input, which is a COMPATIBILITY_POLICY candidate. The chain:

- **Config:** pathToResource + "tts" (0x006A00C8). Defaults: voice "Ryan" (0x006A00CC), speed/shaping/pitch 100, cozmo "Cozmo".
  tts_config.json supplies voice, speed, shaping and cozmo; pitch is never read. android.en = Ryan, speed 50, cozmo
  `\Prn=k_h O z m @U\` (ctor 0x0069FA7C, decomp only).
- **Credentials:** GetUserid = 0x6BA483B0 ^ 0x0ACADADA (0x0069F8DC..F6); GetPassword = 0x0AC94A41 ^ 0x0ACADADA
  (0x0069F8F8..912); GetLicense decodes the 0xE5-byte string at 0x00C00713 (0x0069F914).
- **Java load:** getVoicesList, setLicense, load(voice, ""), LEADINGSILENCE 50, TRAILINGSILENCE 50
  (CozmoTextToSpeech.java:89-119).
- **Name:** "Cozmo" is replaced case-insensitively with the cozmo string (0x006A03F6).
- **Rate:** GetSpeechRate = clamp(speed / d, 30, 300), with d = (scalar > 0.05 ? min(scalar, 20) : 0.05)
  (0x0069F870..0x0069F8D0), cast to int at 0x006A0442.
- **createAudioData(text, rate, shaping, pitch):** 22050 Hz mono (0x006A02BA, 0x006A02D6). On the Java side: setSpeechRate,
  setTTSSettings("VOICESHAPE"), setPitch, speak, then poll isSpeaking every 100 ms (CozmoTextToSpeech.java:121-156).
- **Samples:** the JNI callback appends int16 samples to the vector at global 0x0105C3D8 (0x0069FA00..0x0069FA38).

**Probe.** libacattsandroid.so was loaded at 0x30000000 and its JNI natives driven in the Java order. "Hello, I am Cozmo." gave
70,992 samples at 22050 Hz (3.22 s), byte-identical over three runs. With bogus credentials the load returns -12, so the licence
is enforced.

**Speed:** the voice loads in 4.3..5.4 s, once. Synthesising the 3.22 s phrase takes 544M instructions and 12.3 s, with memcpy and
memset kept inside the emulation: about 3.8 times slower than real time.

**Stand-ins written for the probe:**
- a virtual file system: fopen/fread/fgets/open/read/mmap and opendir/readdir;
- an sscanf subset;
- qsort with nested emulation;
- time and clock_gettime;
- about 20 JNIEnv functions.

lrand48 is imported; its effect on the output is UNKNOWN.

## Records (no change yet)

- **M11-016** (RECOVERABLE_GAP): its evidence names symbols only, so it is too weak to cite. OKAO ships inside the engine and runs
  as-is.
- **M14-006** (BLOCKED_EXTERNAL, "Text to speech is not implemented"): contradicted in practice. The shipped library and voice
  synthesise under emulation with the engine's own licence. The route through the app's Java class is not recorded.
- **M14-010 / M14-011** (IMPLEMENTATION_GAP): their premise, "nothing beyond the seam is buildable", no longer holds if OKAO runs
  under emulation.

## Open

1. A real-face capture from the operator.
2. The Java glue as a COMPATIBILITY_POLICY "input the app supplied".
3. Running FaceRecognizer synchronously in the emulator, which would be implementation freedom.
4. A strategy for TTS speed: caching or pre-rendering phrases, or a faster emulator path.
5. Unread instructions: FaceRecognizer::Init 0x00862A18, ExtractFeatures 0x00865034, the TTS ctor 0x0069FA7C arguments.
6. Why the PT confidences are 0.
