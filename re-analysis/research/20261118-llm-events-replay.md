# LLM-EVENTS-REPLAY-1 — evidence and replay limits

## Input boundary

Available frames are a fixed 164169463-byte file, 124247 rows, ending at
1006054.0132 ms (16.8 minutes); SHA256
7ec89bde9b09402497fb9e44dba403f43e0c12ee19fec0d5822a6d92576f3d52.
The server still owns UDP 47817 (PID 107304) and keeps appending idle events after
that range. It has not been stopped. The numeric fixture freezes events only
within the available frame range, not the later idle tail. No raw frames, images,
face names or hardware bundle are included in the fixture.

`llm_event_fixture.py` extracts CLAD float32 bit patterns and packed-body hashes.
This is explicitly a **recorded-stack cross-check, not native source authority
or a native engine oracle**. Its counts must not be promoted to native truth by a test pass.

Within that range the recording has 264 face observations, 5 ID changes, 5 face
loss reports, 146 cube observations, 704 charger observations, 15 filtered taps,
1 movement + 3 stops, and 1 object-pose loss. All 264 face names are empty: positive
persistent/session IDs do not make a named recognition. Face-recognised is absent;
the next operator session must teach/use a named face to cover it.

Both historical polls report gap=false (121 rows then 113, final cursor 234).
The two live read_events polls contain 49 face observations, 2 ID changes,
2 losses, all 146 cube observations, 13 taps, all 4 movement/stops and the pose
loss. They do not poll the later face/tap/charger windows. Absence from those two
polls alone is not missing delivery. Taps are E2G 0x13, not 0x11 (up-axis).

## Full production path exercised by the private replay

Every incoming captured datagram -> real ReliableTransport offline receive /
sequence acceptance / multipart assembly -> normal receiver -> cold robot
connection/firmware/body/SyncTime handling -> recorded NV replies (including the
actual camera calibration) -> real F0/F4 history and F2 assembly -> asynchronous
vision worker, shipped native detector/recognizer and normal world consumers ->
engine E2G and typed report FIFO -> bridge -> serialized in-process MCP read_events.
The existing offline host-ready seam is used (StartOffline startReady=true);
production StartHostEngine/real engine thread startup is not replayed. The robot
connection itself starts without ToSynced or a preinstalled calibration.
Recorded action calls also cross MCP; frame/image/face/cube results are not injected.
A no-sound device boundary keeps the real audio readiness path open; it uses the
existing 48000 Hz stereo s16 device timing, while robot feedback remains captured.

The recorded RNG seed is 3880367427. ObjectID's process-wide bss/map start empty
(0x0105DFF0 / SetID 0x004EF468); the nonparallel ObjectID collection resets that
fixture initial state, rather than allowing an earlier test to redirect literal
recorded world IDs. No in-run production ID reset or argument remapping is added.

Regular 60 ms replay ticks with a guessed phase batched NV replies before their
next request, leaving calibration absent. MoodState is once per eligible
Robot::Update mood phase (0x00513E84..8A -> MoodManager::Update 0x0067B5D4,
SendEmotionsToGame at 0x0067B6A4; union 0x61). The captured mood publication times
retain startup catch-up turns, and restore the NV request/reply ordering. This is
a **recorded host-clock input**, not a claim that publication time is the exact
original tick-begin timestamp.

## Source read / scheduling uncertainty

Processor 0x00651F08: under its mutex the encoded-image vectors and VisionPoseData
are swapped (0x00652018..0x00652212). Current input is tested at 0x0065221E..24,
UpdateVisionSystem is called at 0x0065222E, and after it returns the current and
pending vectors are both cleared (0x00652234..0x0065224C). The loop sleeps 2000000
ns at 0x0065225E. Selection of available pending input therefore depends on worker
availability; work arriving during processing can be discarded at completion.
The source supports a family of traces, not a unique output count from frame bytes
alone. M11-040 remains IMPLEMENTATION_GAP; no settlement is claimed.

Diagnostics confirm that adding a per-tick worker-idle fence changes selected
images: over the first 100 seconds the live capture processed 1382 images, emitted
49 faces and 52 cubes; a fenced replay processed 1383, emitted 63 faces and 51
cubes. A naturally asynchronous replay processed 1171, emitted 40 faces and 39
cubes. These numbers are **observations**, not a tuning target. The final replay
uses the real asynchronous mailbox. No forced frame selection, output injection,
synchronous-mode pause, or invented worker-duration model has been built.

## Native event contract and actual correction

- Face observations: M14-008 is EXACT_SOURCE, title `Vision face-result handoff
  and FaceWorld lifecycle broadcasts`; current evidence includes UpdateFaces
  0x006551E8..0x00655274, AddOrUpdateFace/expiry 0x004F5316..0x004F54A8,
  observation broadcast 0x004F4B7A/0x004F4B82, deletion 0x004F3C2C..0x004F3C64.
- ID changes: FaceWorld::ChangeFaceID 0x004F3DB4 loads old ID/new ID as two
  signed words at 0x004F3DCC, stores them at 0x004F3E54, constructs the
  RobotChangedObservedFaceID union at 0x004F3E5C and broadcasts at 0x004F3E64.
  This tail is reached even for an unknown old ID; absence from the face map does
  not by itself suppress the two-word report. No new lifecycle code changes.
- Object observations: AddAndUpdateObjects 0x00620AD4, observation/broadcast tail
  0x006211A6..0x00621214 and BroadcastObjectObservation 0x0061FED8; Pack 0x007131E2.
  M11-038 still IMPLEMENTATION_GAP; it is not upgraded here.
- Movement/stops: M4-009 remains EXACT_SOURCE, title `HandleActiveObjectMoved/Stopped/
  UpAxisChanged: lookup, charger filter, double-tap window, IsMoving on change, the
  broadcasts`. Actual moved build/broadcast 0x00534190..0x005341B0 copies timestamp,
  world ID, accelerometer and input axis byte. ObjectMoved::Pack writes that byte
  at 0x007B01F2..0x007B0200. Stopped has only timestamp/world ID.
- Filtered taps: BlockTapFilter Update 0x006310BC, broadcast 0x00631178..0x00631192
  (also 0x00630C4C..0x00630C72), E2G 0x13; M4-023 remains EXACT_SOURCE.
- Pose loss: ObjectPoseConfirmer::MarkObjectUnknown 0x00507128, broadcast of the
  original object ID at 0x005073AA..0x005073C0; E2G 0x52. M11-007 remains
  IMPLEMENTATION_GAP; no broad-path settlement follows from the replay.

The bridge snapshot already retained movement's axis but omitted it from JSON.
The bridge now includes `axisOfAccel` for a moving report, null for stop/tap;
controller state and traffic are unchanged. Existing nested radio slot IDs remain
radio IDs; the native event world ID is cube.worldObjectId. The independent test
writer reconstructs the CLAD body using that world ID and checks every field and
order against the replay's emitted E2G body, without production PackBody/serializer.
This is an **integration delivery witness**, not an independent proof that every
upstream world/detector decision is native. The full raw-input replay and historical
capture comparison must retain that distinction.

Validation: first 10 s installs the actual NV calibration and drains startup FIFO;
first 100 s has native speech ready/PlayOnRobot and all action calls return. The first whole-window run passed all 4 focused cases in 16.8678 minutes:
94048 incoming packets, 14653 assembled camera frames, 12617 processed frames,
zero handler faults, readiness and native audio PlayOnRobot reached. Replay E2G:
face seen 222, ID changes 4, face losses 6, cube seen 110, charger seen 580,
taps 15, movement/stops 7, object pose losses 2. These differ from the historical
recording; delivery to MCP matched every replay emission byte-for-byte and in order.
There were 95 unique existing MISSING diagnostics, including direct world/owner
binding gaps; no MISSING-free or full upstream source-fidelity claim is made.

The final fixture supplies host ticks/seed and validates the raw frame SHA256.
All 217 live-polled relevant events are projected independently to packed bodies:
216 match; the moving row at cursor 229 lacks axisOfAccel. All retain engine order.
A final 10-second startup/fixture check plus the 3 bridge tests pass (4/4).
The final full suite passes **8742/8742 in 44.1340 minutes**, all 16 private
cases enabled. Final whole replay: 94048 incoming packets, 14653 camera frames,
12100 processed frames, zero handler faults. Its 854 E2G emissions match MCP
body/timestamp/order: faces 218, ID changes 1, losses 5, cubes 82, chargers 524,
taps 15, movement/stops 7, pose losses 2. All recorded occurring tags are exercised.
Its 95 unique existing MISSING keys and the independent native occurrence-oracle
MISSING remain explicit; no whole-path acceptance follows from delivery success.

Re-extraction is byte-identical while the live log continues growing. Fixture
SHA256 with Windows line endings: be463bed8ca3ee84ee08ae99735ac84c80b59aba447cd2f59d22dcbcaecdeff3.
`fidelity.py --check` passes (764 records); tested source/fixture hashes unchanged.
No manifest/inventory/SCOPE/PROJECT_STATE change or robot acceptance is claimed.
