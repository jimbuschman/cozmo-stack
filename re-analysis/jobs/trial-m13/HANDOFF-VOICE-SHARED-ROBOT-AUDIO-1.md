# VOICE-SHARED-ROBOT-AUDIO-1

## Native recipient

- The opt-in shared owner creates a blocking StreamMgr device with cache and 0x180000 bytes (0x9604FC, 0x96060C, 0x961168, Anki setup at 0x8D6C02). Native stream scheduling and caching stay in the guest. The managed hook reuses the local ZIP index and returns the native descriptor and defer flag (resolver 0x8D6CC6 and 0x8D6DBC, retry 0x963890 to 0x960BDC, read 0x8D6E88, close 0x8D6F46). The emulator steps the blocking worker body at 0x966A7C when the gates at 0x97CA90 admit work. Native engine termination joins audio and bank workers (0x99DCEC), then the resolver is cleared and the blocking device and manager are destroyed (0x9606C4, 0x9606E8, 0x961184, 0x960398).
- A generation can offer speech and set Name and pitch before the authored event (0x8DC1B9, 0x99FE1C, 0x99FA24). The WavePortal consumes the pending data at 0x8DD636. A separate clear frees only the unconsumed offer (0x69ECA4 to 0x8DC228 to 0x8DD608) and stays available before the animation abort.
- The recipient posts each authored event to native object 7 and retains its own cookie and playing ID (0x9818C–0x982A0 to 0x9A6704), with distinct per-playing volume and ProcessEvents commands (0x99FB14, 0x99F130). Guest callback delivery is deferred until the guest call returns. Generation fences prevent retired callbacks or new submissions from reaching a later clip. Already accepted volume and render work remains ordered ahead of the stop barrier.
- Hijack open, sample output and close are independent of individual event completion (0x8DBD98, 0x8DC040, 0x8DBDF8). The platform thread retains the same recipient, sink and robot buffer across clips. Shared abort runs native StopAll for object 7 followed by ProcessEvents (0x59678E–0x5967B8, 0x9A6064, 0x99F130). Closing the platform owner drains active native events and Hijack before teardown. Natural generation retirement leaves the owner alive.
- The shared guest clock preserves its native low word across wrap. The existing single-event direct owner still uses its prior open path.

## Files and checks

- NativeWwiseArchiveIo.cs adds the guest resolver and blocking-I/O hook. WwiseAnkiResolver.cs binds it to the guest device. NativeWwiseVoiceHost.cs owns the guest worker, callback ledger, shared WavePortal and Hijack. NativeWwiseVoiceSession.cs exposes the serialized generation recipient for WwiseRobotAudioPath and drains shared native work on shutdown.
- NativeWwiseVoiceHostTests.cs drives the six actual events and offsets from shipped AcknowledgeFaceNamed clip _01 through the real native engine with the local archive. It compares the two clips in one owner and a StopAll/replay against a separate original-engine run. The ordinary clips produced 128 and 129 frames of 744 floats, SHA-256 8a606148ed6b0c36576e18901194f03ca04c7649c71cd0bd629be84c9876641c and 4857a25f655c233eb103142db7e0073e809e366c9dc37faef30a6efcbda0f42e. Stop and replay produced 33 and 129 frames, 4c6071f9ac6a1002b69142badbfc49f77784f0ca0d5afc0690f37c7528d7ae73 and 0c0242dc9bf6a71236a77c1b5332ca79a11a56b9fa6934968f6ca01d5b0d7f28. The manager independently reproduced the ordinary pair in the original-engine harness. The output is a local test comparison and no shipped banks or native libraries are committed.
- NativeWwisePlatformTests.cs runs the generation adapter and active shutdown through the serialized owner with explicit fake device completions. WwiseRobotAudioPath.cs and WwiseRobotAudioPathTests.cs fence scheduled posts and callbacks by clip, including callback delivery after a reset.

## MISSING

- The live stock behavior, animation scheduler and retained SayText child join is VOICE-CANNED-NAME-DELIVERY-1. This job establishes the opt-in recipient. The full WwiseRobotAudioPath-to-session reuse cycle and pending-offer cleanup through the live SayText destructor remain to be checked in the caller job. The isolated differential uses fixture speech and fixed platform completions. Physical robot audibility and original Android device scheduling were not exercised. No fidelity status or approval changed.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.

Affected tests passed 43/43. Independent manager affected run passed 158/158 with zero skips. Independent source review found no remaining blocker. Batch full gate passed 7918/7918 with zero failures or skips on f36ae996.
