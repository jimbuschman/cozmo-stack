# PICKUP-PYRAMID-2

- Branch `codex-m13-manager-2`; native execution `47cf34a4` (builder `5287ada4`), camera correction `1ad0d4be` (builder `e379c3a1`), notification scope `d71e12c7` (builder `cc5a5480`). Independently code-verified and exercised through the shipped configured instance; not finished/hardware-verified.
- Major files: new `Manipulation/NativePickupObjectRunner.cs`, `Vision/NativeObjectRunners.cs`, `NativeObservedMarkers.cs`, `NativeObservedObject.cs`, `Actions/ObjectInteractionCompletion.cs`; helper/ActionList/compound/watcher/API, Docking, BlockWorld, history/camera and Sensors bindings; `PickupPyramidNativeExecutionTests.cs`.

## Changed claims / binary addresses

| Claim | Primary evidence |
|---|---|
| Actual helper first turns retain supplied maxTurn/normal104=true and ignore-failure flags; Pickup proxies its real tag; original full completion survives response animation. |005B7A4C/005B7B48..7E66;005B7482..7524;checked PK1–10|
| Owned TurnObject uses ANY32767, native f32 MAIN-relative marker selection, mandatory refinement/tolerance/second cycle; owned VerifyObject type2E/tracks1 uses real44 and Wait43. |00549DC0/0054A1F0..A710/0054ADD4..AF66;00C9852E;005686D8/00568A7C..C24|
| Canonical native marker ctor alone initializes timestamp0; observed query uses unsigned per-marker stamps, since0 empty. Public unknown inputs remain unknown. LOCAL44 follows native object/marker/occluder updates; full host44 stays MISSING. |0087E276..27C;00876C50..96;00621144..1E6;0061FED8..62007A|
| Real engine-ticked Pickup/IDock type15→16/17/tracks7/F7false, retained prepared children, load request, zero-delay same-tick Verify and raw-state gates replace the missing native execution. |00553648..700/00553A1C..B2E;0055030A..3DA;005514FC..51C4C;005521AC..23F4/0055738A;00553BE0..54056|
| Source reaction/light/squint setup and ordered retirement: own children, deselect, conditional path/dock abort, marker3/target−1, lights/squint, locks then device handles. Abort itself remains send-only; late native B8 cannot attach a stale retired target. |00551948/00551BEC..51C4C;00553750..790;00557448..75B2;0063BE10..5E;00632CB4..CC4/00533848..850|
| Typed immutable22-byte object result survives live/prepared/current-tag cache/proxy/watcher/API/original callback; carrying iteration overwrites word0, normalflag0. Undefined public body is skipped visibly under M1-034 while defined internal flag/scalar completion remains. |005537D4..5395C;00540750..7C0;0054F050..F130/0054F170..F3EA;0053F9E4..FB06/00541BB4..1C04|
| Synced raw docking bit2 reaches the actual owner after treads/before charger, including unknown-origin states. Historical marker camera uses SAME full computed history/head/exact parent and checked geometry; separate face camera input is preserved. World notifications/diagnostics release before caller docking and current-frame face mutation. |00512A96..AA0;00513A66..B16;00654E82..E8E;00654E12/E38..E4A;006544A2→00654510|

- Standalone error machinery: unavailable speech clears callbacks and safely retires only its owned tag, independently of UI processing; unrelated actions survive. Pose gates end before public callbacks. AGENTS.md permits host error/ownership mechanisms; no invented native ActionResult.

## Verification

- Primary/read/build packets: `20261027-pickup-pyramid-2-{read,read-codex-check,codex-check}.md`; exact commands/failures in builder report.
- Independent700/700 PASS35s on original frozen candidate; affected correction independently92/92 PASS10s,0skip. Other25 hashes unchanged; final two replacement hashes recorded in check.
- Actual shipped factory→manager/parent84/helper(normal104true)→owned turns/motors→real worker44→dock/raw Active+DA/B8→Verify/carry→typed original callback and success reaction. Scene/calibration/firmware/detector inputs supplied, not hardware. Separate units cover carrier overwrite/undefined publication, response retention, late B8, unsigned stamps, historical camera, callback gates and UI-disabled error cleanup. No injected5A in configured positive case.
- Final asset-complete full gate: **6421/6421 PASS,0 failed/0 skipped,exit0**,6m35s; clean `d71e12c7c40cf95d9bf68c2dbc65843bf7a1bebe`, UTC2026-10-27 10:26:59.8660209..10:34:14.2460322. Logs `.scratch/pickup-pyramid-full-{3.log,console-3.log,3.json}`; fingerprint `eafea7c8d36ac8c823fcf4c15da2add1842b9c2f`. Independent final affected107/107 PASS10s,0skip after preserving original X16 ordering assertion; other25 hashes unchanged.
- First attempt interrupted for the unverified face-output change (no completed count). Second full gate onffe13e1b:6420passed/1failed/0skipped,6m48s; notification ordering regression. Both scope corrections and their affected independent checks precede the final gate.

## RECORDS TO SETTLE / leftovers

- **M12-018 IMPLEMENTATION_GAP**, “The dock abort path”; evidence0063BE10/BE2A/BE36: propose settlement. Send-only/raw bool, pending request preservation, real native destructor caller, separate request reset and late-B8 regression are source/code verified.
- **M13-020 IMPLEMENTATION_GAP**, “PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction”; evidence00549C48..D60/0054B010..1C/0054CA64..DE38: native executor now verified; own four setters005498A0/5499AC/549AE8/549B0C remain unbuilt. Claude decides own supported-input boundary; retain existing M13-026 libm classification, no phone-last-bit claim.
- **M12-007/017/034/037**: native pickup slice built; supported public/manual Verify timing, general IDock setup/cleanup,4C producer, code-list marker adapter and stand-in subactions remain their own gaps. Current literal titles/status/evidence are in independent check/manifest; no whole-record proposals.
- **M12-019 IMPLEMENTATION_GAP**, “The docking-error-signal clamp is 40 degrees”; evidence0063C182/C188/C194: exact native clamp/Rodrigues and required signal inputs remain MISSING. No legacy double/PnP/latest-history substitute.
- Named speech, DriveTo and no-marker Search remain actual-branch MISSING (Search next inP5); full host44, proprietary detector/PnP/passive input and enclosing Spark/BuildPyramid admission remain open. No official status/approval/acceptance edits.
- Claude robot script: start shipped pickup with pre-dock cube; capture first turns, LOCAL44, docking payload/load request, Active rise/fall, DA/B8, carry/result/response and stop/late-B8 cleanup. Expectations at addresses above; unavailable paths visible; robot run never holds following work.
