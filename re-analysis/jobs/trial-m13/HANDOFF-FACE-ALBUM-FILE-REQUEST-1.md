# FACE-ALBUM-FILE-REQUEST-1

## Claims and evidence

- VisionComponent binds the live G2E 0x8F request when the external interface is present at 0x0065019A–0x006501C0 and 0x00650734–0x00650766. The CLAD path has a byte-length prefix followed by a bool, defaulting true at 0x0073869A–0x007386AC. The C# command queues the same input shape against the original owner, vision recipient and initialization epoch. Stale or unavailable owner work cannot transfer to a replacement.
- Handler 0x0065827C–0x006582A8 calls 0x006580C0. Flag true joins config/basestation/faceAlbums and the requested name through FullFilePath at 0x0080315C, then DataPlatform scope 1 at 0x0084BE66–0x0084BE80 and 0x0084BED8–0x0084BF16. The host ResourcesPath is the existing scope input. The false branch forwards the path at 0x0065812C–0x00658150. The shared importer appends /data.bin and /enrollData.json as the original does at 0x00869432 and 0x00869692.
- At 0x00657936 the request calls the load overload, which reaches the existing stock file importer through 0x0065796E and 0x006B9D8A. A defined file failure logs at 0x00657992 and neither starts a save nor replays the staged list. A successful load branches at 0x006579C6 through 0x008CD0DC to SaveFaceAlbumToRobot at 0x006570A8. The queued save serializes the adopted state. This request has no startup pre-erase and does not roll back a loaded album if save admission fails.
- The save checks the original size limits at 0x006571E4–0x0065720E and rounds vectors up at 0x00657360–0x0065737C. It queues album before enrollment. Writes at 0x006573A6 and 0x006573E6 require both admissions. For empty data the first erase must be admitted at 0x00657474. A second erase rejected at 0x00657600 warns and returns zero through 0x00657660–0x00657694. Successful initiation does not wait for robot NV acknowledgments. Existing later NV result reporting retains its original owner.
- The shared raw restore at 0x00867384 accepts a nonnull returned guest handle and validates both registered counts. A nonzero helper result at 0x00869682 skips file metadata but still reaches candidate installation at 0x00869B78. The NV caller aborts the same result at 0x00867F7A. The original ARM probe injected a total-data count failure on a real empty guest and verified file install without metadata and NV rejection without changing its prior album.
- Only result zero reaches replay at 0x0065793C–0x00657942. Replay produces one empty 0x58 then the staged lexical 0x1C list at 0x0065159C and 0x00651612. The app queue precedes local delivery at 0x00511DE8. The request carries the same owner and session across all report callbacks.

## Files and tests

- Vision/FaceAlbumFileRequest.cs contains the explicit queued command, original path resolution and save-admission result. VisionSystem.cs binds and unlinks the request and shares the ordered save core. Vision/FaceAlbumFile.cs retains the original file suffix append for both callers.
- The manager executed the original ARM request wrapper 0x00657924 with controlled load/save returns. It observed load-only failure, load then failed save with no replay, and load then save then replay on success. Original 0x006580C0 resolved relative, absolute-looking, empty, dot and slash names with controlled DataPlatform input. Original 0x006570A8 exercised all six first/second admission combinations, including the second erase success return and 3/5-byte inputs padded to 4/8. The controlled returns were fixture boundaries.
- FaceAlbumFileRequestTests drives the queued C# command with a fake robot and real stock guest file, verifies album/enrollment admission before 0x58 and 0x1C without waiting for acknowledgments, and distinguishes rejection branches. It also checks replacement before and during dispatch, same-owner session supersession, the external interface gate and the root filename for an empty direct request. M5X13BuilderTests checks one unlink for the new host subscription. The builder passed 590/590 affected before the shared candidate correction and 92/92 focused afterward. The manager independently passed 80/80, including the unchanged native OKAO and Acapela oracles.

## MISSING

- Host paths that cannot be represented by the command and missing resource or guest owners are diagnosed. Unusual Android-versus-host filesystem mapping and actual hardware are outside this job. The startup selectors keep their separately verified order. The managed suite does not inject guest count failure; the native differential uses controlled return status.

## Records and workers

- M3-062, M14-011 and M14-012 retain their current statuses and approvals. Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults.
