# I-M15 gap pass 2 — serial-number trigger

Read-only extractor pass, 2026-09-28. The search covered the exported/thunked and
body forms of the relevant functions, all direct Thumb `bl`/`blx` targets in the
executable segments, Ghidra's caller index, and the RobotInterface message-handler
wrapper.

Recovered facts:

- `RobotManager::ConnectRobotToNeedsManager(serial)` tail-calls
  `NeedsManager::InitAfterSerialNumberAcquired(serial)` (`0x0052FADC`).
- `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` is an eight-byte
  wrapper at `0x0069DEE4`; its thunk is `0x004A9B2C`.
- No direct call to the body, thunk, or RobotManager wrapper exists in the executable
  instruction stream, and the decompiler caller index has no caller for the body.

The remaining edge is therefore an indirect/generated message-dispatch registration
or a caller outside this native image. Its message/tag and registration point were not
established. This remains a live-path `RECOVERABLE_GAP`. Exact remaining work: recover
the RobotInterface generated dispatch table or registration data that owns wrapper
`0x0069DEE4`/thunk `0x004A9B2C`, then identify the inbound message field carrying the
serial and its call order relative to robot connection.
