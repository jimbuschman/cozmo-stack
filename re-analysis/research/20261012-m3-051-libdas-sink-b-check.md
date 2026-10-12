# Opus check of R-M3-051 (2026-10-12)

- **Report:** `research/20261012-m3-051-libdas-sink-b.md` (5f4f04d2). It was checked statically with lief and
  capstone; the libDAS SHA matches the report.
- **Result:** 29 rows CONFIRMED, 2 WRONG (B24, U01), 1 UNVERIFIABLE (U02, the server contract).
- **Provenance gap:** the local dumps the report cites were not committed.
- **Adopted:** by the manager on 2026-10-12, with the operator's ruling below.

## Additions to the report

- **B12:** `\u` escapes use uppercase hex (0x1F21E `orr 0x4000`).
- **B14:** if the file owner is null, the record is lost silently, but the timer is still armed.
- **B16:** the roll comparison is unsigned.
- **B25:** an oversize file also logs `dasappender.consumealogfile.filetoobig` (level 4) via `_DAS_Logf`
  (0xB9B0..0xB9EC). With the shipped limits it is unreachable.
- **B26:** 0x27C3C is a JNI call into Java `com.anki.daslib.DAS.postToServer` (app layer). The file is unlinked on both
  success and failure.

## Correction (B24 / U01)

`cozmo_startup` calls `DASSetArchiveLogConfig` (engine 0x665A4E) before DASConfigure, with:
- an archive callback at 0x666C04 (CreateArchiveFromFiles 0x666CEA, then DeleteFile 0x666D2A);
- an unarchive callback at 0x666E44 (CreateFilesFromArchive 0x666F48, then DeleteFile 0x666F8A);
- suffix ".tar.gz" (0x105B670, from 0xC49ED0 at 0x4D80A2).

So files are archived, unarchived and skipped as `.das.tar.gz`. The report assumed an empty callback.

## Engine calls into libDAS the report missed (all live)

- **Provider:**
  - 0x6671F4 → DASForceFlushNow;
  - 0x667214/0x66722E → `_DAS_SetGlobal`;
  - 0x66721C/0x667236 → `_DAS_GetGlobalsForThisRun`;
  - 0x667222/0x66723C → DASEnableNetwork(reason);
  - 0x667228/0x667242 → DASDisableNetwork(reason).
- **Pause:**
  - startup 0x665F4E Pause(false), which flushes;
  - `HandleMessage<ConnectToRobot>` 0x4ED114 Pause(true);
  - `RobotManager::RemoveRobot` 0x52F2EE Pause(false), which flushes.
- **Network mask:**
  - startup key "DataCollectionEnabled" (0x6659E6) selects Enable/Disable(2) (0x665C1E/0x665C26), before
    DASConfigure (0x665CD6);
  - `HandleMessage<RequestDataCollectionOption>` 0x4EE084 (from the app's DataCollectionPanel.cs:74);
  - `cozmo_shutdown` 0x6668E2 DASDisableNetwork(4);
  - any set bit blocks the upload (0x8818/0x882C; gate 0xB49A).
- **Flush with a callback:** DASForceFlushWithCallback (libDAS 0x717C → 0xB7BC; closure 0xC374 runs
  `cb(!failed, response)`), called at startup 0x665FCC and from DasTransferTask::OnTransferReady 0x6A25FE.
- **Platform init:** DASPlatform_Android::Init 0x665EEC, InitForUnityPlayer 0x665F14, DASNativeInit 0x665F3A.
- **Globals:** sSetGlobal `$player_id` (an MD5 hex digest) at 0x665FAC, and at 0x52F2D8 in RemoveRobot.
- **SDK only, so unreachable:** DASGetLogDir at 0x668210/0x668244.

## Operator ruling (2026-10-12)

Sink (b)'s on-device files and upload are not built: AGENTS.md, departure M3-051. Nothing in scope reads them. The
engine-side control seam above is built exactly (M3-055). A flush with a callback reports a failed transfer. The
data-collection inputs are an app-boundary COMPATIBILITY_POLICY (M3-056).
