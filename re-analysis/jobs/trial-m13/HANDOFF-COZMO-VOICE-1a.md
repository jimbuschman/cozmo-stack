# COZMO-VOICE-1a

## Source inventory for the build

See sections A–C of research/20261103-cozmo-voice-extraction.md. The independent read
confirmed these corrections and joins in the shipped engine and app.

- The style and intent factories at 0x0052AB00 and 0x0052AB50 share the action. The
  metadata loader 0x005602D8 is first-definition-wins. The intent ctor uses UTF-8 byte
  count, duration then pitch, and the Robot RNG (0x00561100, 0x00561142, 0x005126B6).
- Speech operations use the nonzero u8 ID and async state publication (0x0069E4C8,
  0x0069F430). Dispatch priority 2 at 0x0069E3BA is a priority, and its name is
  TtSpeechComponent. Style 2 appends a question mark and s16 converts by 1/32767 at
  0x0069EF10. The provider selects the Android config object at 0x0069F7D4, reads the
  locale at 0x0069FBA2 and the voice, speed, shaping and Cozmo substitution fields
  through 0x0069FC64. Pitch stays 100. The shipped instructions at 0x0069F870 preserve NaN, and the provider at 0x006A0442 converts it to integer zero. Other rates clamp to 30–300.
- The Java wrapper applies rate, shape and pitch, stops before speaking, polls
  speaking, then stops again. CozmoTextToSpeech.java 121–155 and acattsandroid.java
  491–536, 584–600. Native callback type 1 clears speaking when the queue is done
  (acattsandroid.java 362–369). The Java wrapper checks its own speak result.
- Acapela qsort PLT 0x0001FA40 is reached with four- and eight-byte elements, for
  example 0x0002C15C and 0x0002C1A0. Comparator pointers address the live array.
  Bionic uses insertion below seven and unstable equal-key partitioning from seven.
  ARM32 time, seed and first draw at 0x000310FA–0x0003110A seed the Expat parser
  hash salt (0x00031B90, 0x0002E660). The other observed draws are eval-gated.
- SayText Init state zero or failed prepare aborts at 0x00561630. Other non-ready
  states return running at 0x005615DE. The successful prepare clears op ID at
  0x005614C2. Direct and fit actions insert the style audio event at time zero
  (0x0056153C, 0x00561648). Fit uses the existing group selector with cooldown
  disabled (0x00561DFC), appends tracks at global last trigger plus 33 ms
  (0x00577F02–0x0057805C) and uses the 60000 ms guard (0x00561A52).
  The no-fit action uses a lift-safe trigger (0x005616D6). The M5 loader found
  0xBF0BACE1 in all 12 shipped AcknowledgeFaceNamed group clips.
- WavePortal registers at 0x004DD97C as 0x006412C2. English bank Sound leaves
  reference distinct HIRC type-19 source descriptors, which resolve the plug-in
  through 0x00A1EB58 and 0x00A78A24. The bridge transfers one global pending
  buffer at 0x008DD636. Init at 0x008DDB60 returns 1 or 2 and supplies mono float
  provider rate. Execute at 0x008DDC5A returns 0x2D for full frames, and 0x11 with
  zero padding for short frames. Cleanup clears the unclaimed slot at 0x0069ECE6.
- The refill consumer uses the configured size at 0x00A44794–0x00A447A4, then
  passes the data to the pitch intake at 0x00A4477C. 0x00A78510 is the mode-2
  source wrapper. The switch at 0x00561736 and RTPC at 0x0056174A are delivered
  before the animation event. They use object 7 on robot and 6 on device
  (0x00599F62, 0x008D243C, 0x008D2524).

## Proposed record boundaries

M14-006 is currently BLOCKED_EXTERNAL. Propose separate SayText action and intent
metadata, provider and asynchronous lifetime, WavePortal bank/source/consumer, and
the Java app glue input as COMPATIBILITY_POLICY. M14-013 lifetime recipients remain
their own claim. The M14 approval is frozen, so existing records and approvals are
left for Claude integration.

Reader requested gpt-6.1-sol medium. Builder and reviewer use the configured default.
Actual backends are unobserved. Independent review corrected the WavePortal mixed-leaf stop and narrowed the RNG test claim.

## Build boundary and test reach

- AcapelaNative.cs and AcapelaTts.cs implement in-place qsort, seeded rand48, and
  the Java-mode discovery, stop and callback sequence. The pinned-clock real
  production-mode test uses the same 70,992-sample oracle. Phone libc revision,
  Android directory enumeration order, and the independent Java polling schedule
  remain unknown. The qsort baseline is bionic eclair-release libc/stdlib/qsort.c.
- SpeechMetadata.cs, SpeechProvider.cs, TextToSpeechComponent.cs and SpeechOwner.cs
  add the trait/config readers, asynchronous operation store and owner retirement.
  The real robot ActionList reaches SayTextAction.cs through an internal admission.
  CanInterrupt false follows vtable 0x01022DB8 to 0x0052B0B2. Native direct NOW
  ingress is 0x00525A88–0x00525AC4. The external and named-face admissions are open.
- WavePortal.cs models the pending buffer and source refill. WwisePlayback.cs keeps
  the selected bank Sound and Source IDs separate. WwiseAudioSource.cs reports a
  missing source descriptor and float-frame output join while preserving ordinary
  sibling media. It does not consume the pending speech in this case.
- CozmoVoiceTests.cs covers provider rates, real Acapela samples, operation state
  and retirement, the actual shipped voice event's bank leaf, and source tail
  padding. EngineAppLayerTests.cs exercises a real canonical owner and verifies
  that the action stops with the audio-path diagnostic and no AudioSample.

MISSING: production WavePortal descriptor and float-frame/RTPC/mix/Hijack join,
the inner animation (direct, fit and canned trigger) and playback completion,
external and named-face ingress, exact phone discovery/poll timing, and callback ordering beyond the tested phrase (a sample arriving after text-end can change Java speaking state). The
internal action reaches synthesis and stops at the output frontier. It does
not provide audible robot speech. No status, approval or hardware claim changes.

Focused 28/28 passed, including the unchanged Acapela and OKAO oracles and the real production-mode phrase. The exact UTF-8 bucket and draw order have source citations. The test checks shipped trait ranges and does not isolate those two effects.

Batch full gate on 4487b1fa failed (7801/7803, zero skipped). The old stock-detector availability fixture was corrected and its follow-up passed 61/61. The identical SolvePnP oracle mismatch also reproduces on origin/main a9dbec14. The corrected gate ran on 601b4475 after rebasing onto 1771234b. All 7803 tests passed with zero failures or skips. This bounded handoff is READY FOR CLAUDE.