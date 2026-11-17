# SKILLS-WAIT-REACTION-1 handoff

The skills capture records ROLL_OBJECT_LOW refused at 80899.84 ms after ObjectPositionUpdated began at 79567.20 ms, and DRIVE_TO_POSE refused at 129735.03 ms. Both use `ActionQueue.QueueAction.ExternalActionsDisabled`. The native reaction getter is 0x005A19EC. `UpdateRobotPropertiesForReaction` 0x005A2F54 writes Robot+0x2C7 at 0x005A2F60. `ActionList::QueueAction` reads it at 0x0053D94E and logs the external-tag refusal at 0x0053D996.

Both harness modes check the current reaction and that flag before submitting. The harness gives a busy robot up to 15 seconds to become available. A timeout or admission refusal is a harness error without an engine result. Go-to samples the start after the wait, and the tracking cancellation interval begins after submission. The regression checks the two readiness conditions independently during a transition. Both no-robot validations passed with 19 references each.
The focused no-robot suite passed 120 tests.
