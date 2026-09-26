# NV storage evidence (extractor output, not yet an approved inventory)

These are three read-only extractor passes over `libcozmoEngine.so` 3.4.0-1204, made on 2026-09-25. They were saved here because they existed only in one machine's temp folder. They are raw extractor reports: every row cites an address, but no manager has approved them as an inventory yet.

| file | what it settles |
| --- | --- |
| `nv-pass1-calibration-read.md` | The CameraCalib read (0x80000001): Length = `_maxFactoryEntrySizeTable[tag]` = 1; reply acceptance; reassembly at index x 1024; completion; retry (up to 8); the 5 s robot-clock timeout; the VisionComponent callback. M11-011 is contradicted. |
| `nv-pass2-dispatch-gates.md` | What gates NVStorageComponent::Update in Robot::Update (engine Running; the first full state after SyncTimeAck; after a calibration, a BlockWorld failure). Replies are not gated. robot+0x24 is mfgId word 1, the body hardware version. No callback on disconnect or destruction. |
| `nv-pass3-connection-queue.md` | At connection, 12 NV reads are queued before the calibration read (unlocks, inventory, the face album, 8 backup reads from backup_config.json), then Lab and Needs after it. Ready-to-stream waits for the whole queue. DefaultCameraParams never installs a calibration. The array reader has no cap. |

## Hardware record

`vision-calibration-read-20260925-170150.json` is a `Cozmo.Conformance vision` acceptance record from a
real robot (fw 2457, serial 206587486), moved here from the working tree. It is not an extractor report: it is
the hardware observation that the built calibration read requests tag 0x80000001 with `length=1` and installs
the 56-byte blob (`NVOpResult ... index=0 data=56B`, `calibrationRead: true`). The run found no markers in a
90 s stream, so the record's automated `pass` is false; that is the marker detection, not the NV read.

## Where this stands

- **Built on main (2f01813, 1d2ee2c):** only the calibration read and its callback.
- **Not built, and not yet recorded as manifest records:** the startup queue, the Lab/Needs reads, the timeout, the retries, the dispatch gating and the non-factory read path. M3-022 is EXACT_SOURCE, but that covers only the callback.
- **Next step:** an M3 inventory correction (C2) that adds NV records from these passes. Pass 1's open questions include the non-factory read path, which is still to be read (a planned fourth pass). Approve the correction, then build.
