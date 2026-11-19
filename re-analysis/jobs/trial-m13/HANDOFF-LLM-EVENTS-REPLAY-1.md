# LLM-EVENTS-REPLAY-1 — READY FOR CLAUDE (partial; acceptance blocked)

The available local copy is used; no bundle is committed. This handoff does **not**
claim the queue row's independent whole-session native occurrence oracle is complete.
The raw-input replay and MCP delivery check are built; the missing native scheduling
witness is explicit in the replay report and research note. No hardware acceptance,
SCOPE completion, or manifest settlement is claimed.

## Path from input to result

| Step | Evidence / exercised path | Result and limits |
| --- | --- | --- |
| Incoming recorded datagrams, sequence/reliability and multipart assembly | Actual `ReliableTransport` offline decoder -> RCD ReceiveData / PushArrivedMessage 0x0062E274..0x0062E2C2 -> message drain 0x0062F4B4..0x0062F4BC | Every incoming datagram is fed unchanged; no network socket/robot is opened. |
| Cold robot connection, state history, NV camera calibration | Recorded handshake/NV replies; calibration UpdateCameraCalibration 0x006B1E3E, Camera::SetCalibration 0x0085DEDC; F0/F4 history and F2 camera assembly | Uses the captured calibration, not a synthetic image pose or pre-booted action fixture. The existing offline host-ready seam is used; production StartHostEngine/thread startup is not replayed. |
| Host clock and process state | Native 60 ms runner 0x0065B3D2/0x0065B3D8; Mood update 0x00513E84..0x00513E8A / 0x0067B5D4 / 0x0067B6A4; ObjectID bss 0x0105DFF0, SetID 0x004EF468 | The frozen numeric fixture supplies recorded host mood-publication times and RNG seed. Publication times approximate the tick boundary; initial process statics are reset only by the isolated test. |
| Image worker and native perception | Processor 0x00651F08, pending/current swaps 0x00652018..0x00652212, process 0x0065222E, clear both slots 0x00652234..0x0065224C, 2 ms sleep 0x0065225E | Real asynchronous worker, shipped native detectors/recognizer, normal world consumers. No selected-image/result injection or forced completion schedule. **MISSING: independently recovered native occurrence list for this exact worker schedule.** |
| Face seen / recognised | FaceWorld AddOrUpdateFace 0x004F4278, result admission/expiry 0x004F5316..0x004F54A8, E2G 0x46 build 0x004F4B7A, broadcast 0x004F4B82 | Complete pose, rect, ID/name, expression/attributes/scores and feature points are compared byte-for-byte at the delivery boundary. Nonempty name adds recognised delivery. The recorded session has no nonempty names. |
| Face ID changes and loss | UpdateFaces 0x006551E8..0x00655274; ChangeFaceID loads old/new at 0x004F3DCC, builds E2G 0x47 at 0x004F3E54/0x004F3E5C and broadcasts at 0x004F3E64; deletion E2G 0x4B at 0x004F3C2C..0x004F3C64 | ID/loss payload and relative delivery order checked. |
| Cube / charger observations | AddAndUpdateObjects 0x00620AD4, tail 0x006211A6..0x00621214; observation 0x0061FED8, broadcast 0x0061FFDE; E2G 0x44 Pack 0x007131E2 | All 69 bytes compared, including world ID, rectangle, pose, orientation and active flag. |
| Cube movement / stops | Moved construction/broadcast 0x00534190..0x005341B0; Pack axis byte 0x007B01F2..0x007B0200; E2G 0x0F/0x10 | **Fixed** bridge omission of the movement axis; stops have timestamp/world ID only. Controller state, thresholds and traffic stay unchanged. |
| Filtered cube taps | BlockTapFilter 0x006310BC; broadcast 0x00631178..0x00631192 and 0x00630C4C..0x00630C72; E2G 0x13 | Timestamp, world ID and all four tap bytes compared. E2G 0x11 is up-axis, not tap. |
| Object pose loss | MarkObjectUnknown 0x00507128, E2G 0x52 build/broadcast 0x005073AA..0x005073C0 | Original object world ID compared. |
| API delivery | Existing immutable typed report / E2G FIFO -> CozmoBridge -> real serialized in-process MCP tools/call and read_events | Every replay emission's packed body is reconstructed independently from MCP JSON, then checked for payload, robot timestamp and order, with no ring-buffer gaps. This checks delivery, not independent upstream native detector correctness. Original action calls also cross MCP. |

## Numeric recording and historical comparison

Fixture `llm_session_events.json` contains numeric fields, exact float32 bits,
body hashes, input fingerprints, clock ticks and original tool names/times.
It contains no images, raw frames, face names or hardware bundle. The extractor
is `re-analysis/tools/llm_event_fixture.py`. This C# stack recording is a cross-check;
it is **not native source authority**.

The finite recording ends at 1006054.0132 ms: 124247 frame rows, 164169463 bytes.
The still-running MCP process keeps appending idle events after that boundary;
those later hours are excluded, and the process has not been stopped.

Recorded engine output in that range: face seen 264, ID changes 5, face lost 5,
cube seen 146, charger seen 704, taps 15, movement/stops 4, object pose lost 1.
**Face recognised: 0.** Positive session/persistent IDs alone are not recognition.
The next operator LLM run must teach a name and see that named face again.

Both historical polls report gap=false (121 rows then 113, final cursor 234).
The two actual live polls contain 217 relevant events. Independent CLAD field
projection matches all bodies except the single moving event at cursor 229,
which lacks axisOfAccel; all 217 retain engine order. The axis omission is fixed.
The live polls did not cover later face/tap/charger windows; those unpolled rows
are not reported as missing delivery.

## Remaining uncertainty

A whole-window natural replay is supplied, rather than replacing camera/world
input with precomputed reports. Nevertheless raw frames and host ticks alone do
not determine the asynchronous worker's selected-image sequence. The native
Processor explicitly discards pending input after processing, so worker availability
changes the sequence. An idle fence changes the observed counts and is **not** used
in the final harness. No fabricated duration, forced frame list, result injection,
or detector tuning has been introduced to make the counts agree. Recorded robot
feedback is replayed unchanged, rather than generated by a new closed-loop robot
run. Movement delivery also has docking/carried gates (0x0053416E..0x0053417C),
so differing perception/action histories can affect sensor-event counts too;
worker availability is not asserted as the sole cause of every difference.

The replay's own emitted E2G list is an integration delivery witness. Calling it
an independent native occurrence oracle would be circular. The stricter upstream
part of the queue row remains open for the manager's decision/recovery; this job
must not be accepted as whole-scope completion based on the delivery test alone.
M11-040/M11-038/M11-007 remain IMPLEMENTATION_GAP, M14-008/M4-009/M4-023 unchanged.

The first whole-window run passes 4/4 cases in 16.8678 minutes (94048 incoming
packets, 14653 camera frames, 12617 processed frames, zero handler faults, native
audio ready/PlayOnRobot). Delivery of every replay emission matches byte-for-byte.
Replay versus historical counts differ: face seen 222/264; ID change 4/5; face
loss 6/5; cube seen 110/146; charger seen 580/704; tap 15/15; movement/stops 7/4;
object pose loss 2/1. These are observations, not a claimed native expected list.

The replay reports 95 unique existing MISSING diagnostics. Directly relevant
remaining gaps include the M11-037 unobserved-object flat camera/object adapter
(0x00621E8C..0x00622102) and collision/owned-footprint adapter
(0x0062593C..0x00625B88), M11-040/M11-055 VisionPoseData/IMU/pose ownership
(0x0065204A..0x00652200), and M11-077 shared-control NotLastOwner
(0x0084510E..0x0084515E). Startup factory/profiler/diagnostic MISSING lines are
also retained in the private run report; no broad cleanup or settlement is claimed.

The final fixture/startup check and bridge regressions pass 4/4. The **full suite
passes 8742/8742 in 44.1340 minutes**, with all 16 available private replay cases
enabled. There are no failures in this run. The final whole-window case takes
16 m 48 s: 94048 incoming packets, 14653 camera frames, 12100 processed frames,
zero handler faults, calibration/readiness and PlayOnRobot reached. All 854 actual
scope/supporting E2G emissions reach MCP with exact body, robot timestamp and order:
face seen 218, ID change 1, face loss 5, cube seen 82, charger seen 524, tap 15,
movement/stops 7, object pose loss 2. Every recorded occurring tag is exercised;
recognised remains absent. The two natural runs' differing counts reinforce the
unresolved scheduling/whole-path qualification; neither is relabelled native truth.

`fidelity.py --check` passes (764 records); source/fixture fingerprints match the
built inputs. Re-extraction while the live log grows is byte-identical (fixture
SHA256 with Windows line endings: be463bed8ca3ee84ee08ae99735ac84c80b59aba447cd2f59d22dcbcaecdeff3).
No hardware bundle, scope state, inventory or manifest classification changes.

Private validation files (not committed): `.scratch/llm-events-whole-1.json`,
`.scratch/llm-events-final-full.json`, and `.scratch/m13-llm-events-full-90ded15a.log`.
The full run uses an immutable output snapshot, with a 25-minute hang threshold
for the 16.8-minute replay. Commit: this handoff's containing commit.
