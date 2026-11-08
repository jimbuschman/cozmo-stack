# Operator decision pending

- **AI-BODY-DRIVE-CONTROL-1 cancellation.** Native DriveStraight destruction calls the shared PathComponent Abort whenever the path is active, even for a pending action deleted by tag (0x00547530–0x00547562, 0x0053E882–0x0053E8CE). It also always destroys the shared driving-animation state. A public AI command can preserve that defined engine behavior, or use an operator-approved COMPATIBILITY_POLICY that cancels only the drive's own path and animation. An idle check at admission cannot guarantee exclusive ownership after the request is queued. Source-backed construction and line work can proceed while this decision is open.

## Next capability jobs for operator ordering

- **VOICE-SHARED-ROBOT-AUDIO-1.** Establish one persistent native robot audio recipient for ordinary authored events and WavePortal data. Start at 0x00597F12–0x00597F8E, 0x0059818C–0x005982A0 and Hijack1 0x00595E78–0x0059603A. Use a local AcknowledgeFaceNamed clip to check the shared loop start and stop, timed speech, per-playing-ID callbacks and volume, frame boundaries and state across clips against the shipped runtime. Keep the single-speech oracles bit-exact.
- **MEET-COZMO-ADMISSION-1.** Trace high-level activity 3 through 0x005A0F44 and 0x005A11F4 into the shipped MeetCozmo scoring activity. Build the checked selection, runnable gates, owner changes and cleanup through the existing activity entry. Keep the 0x87 named enrollment staging separate until its scan and authored speech children are owned.
- **VOICE-CANNED-NAME-DELIVERY-1.** After shared audio is verified, connect one shipped named face reaction through the actual AcknowledgeFace caller and the retained Name_Normal child. Start at 0x00602966–0x00602AA6 and 0x0054C5AA–0x0054C638. Check the selected authored trigger, all audio keys, face identity, action result and needs-before-turned delivery against the stock assets and fake firmware.

Order and scope are for operator decision.
