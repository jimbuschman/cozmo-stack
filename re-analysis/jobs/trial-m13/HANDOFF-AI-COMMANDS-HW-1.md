# AI-COMMANDS-HW-1

`m15-cube-hw --commands` uses the existing robot connection, camera and evidence bundle. It selects Selection/Wait, runs the 13 requested commands once where their inputs are available, then records the operator's yes/no observations. The 10-second tracking interval starts at queue admission from the script. The native Cancel path stores 0x02000000 for a started action (M8-015, 0x005409DC–0x00540A46). The report compares result codes only. Admission refusals, missing targets, timeouts without a result and operator observations remain ungraded.

The motion, animation and speech rows reference their concrete engine completion routines in the plan and per-phase output. The enrollment result byte comes from FaceEnrollmentCompleted (M14-012, 0x005FE520–0x005FE528). Rename and erase are observed through E2G 0x1D and 0x57 because those reports carry ID/name fields. The harness requests them only for the temporary face successfully enrolled by this run.

The event stream includes timestamped public 0x46 face observations (with their names), 0x47 ID changes, 0x4B face deletions, 0x44 light-cube observations, movement and tap events, and 0x52 object-pose loss. Sources are M14-008, M11-038, M4-009 and M4-023. Existing engine gaps remain on their runtime paths. Missing speech or enrollment support is reported in the bundle.

The no-network dry-run printed all 13 commands and validated 17 record references. The skills, personality and Freeplay-control modes also validated offline. The harness changed the operator script only. Manifest classifications are unchanged. Operator guide: `re-analysis/acceptance/hardware/scripts/ai-commands-hw-1.md`.
