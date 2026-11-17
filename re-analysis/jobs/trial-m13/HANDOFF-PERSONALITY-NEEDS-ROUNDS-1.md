# PERSONALITY-NEEDS-ROUNDS-1

Three selectable steps temporarily apply (Repair, Energy, Play) through G2E `0xC6`, report the named E2G `0x87` behavior start, and restore all three saved values after the window. The C6 handler clamps the three values, stores them and emits `NoAction` (`0x00697E78..0x00697EC6`, M15-048). The harness snapshots under engine ownership after the stored data load (M15-014). Restoration uses the same handler in a finally block. The bundle records both input vectors. The authored behavior's wait is separate from the operator's yes/no observation.

- **ExpressNeeds:** (1, 0.4, 1). Energy Warning begins at 0.21 and Normal at 0.60 in `needs_config.json`. The actual bracket scan is at `0x0069C12C`. `needs_MildLowEnergyRequest.json` wants Warning with a level-dependent cooldown, checked at `0x005EE570..0x005EE5DC`. The run allows 75 s for the named start and stop.
- **PlayAnimOnNeedsChange:** (1, 0.1, 1). Energy is Critical. `needsSevereLowEnergy.json` admits the activity through the Critical bracket predicate (`0x006141A0`). Its chooser tries `Needs_SevereLowEnergyGetIn` first after the drive-off and put-down checks, with the class gate at `0x005BFECA..0x005BFEF6`.
- **DriveInDesperation:** (1, 0.1, 1). The same activity chooses `Needs_SevereLowEnergyState` after GetIn Stop marks Energy as expressed (`0x005BFEF8`). Its own runnable result is true (`0x005D90AC..0x005D90AE`). The step requires a clear area for movement.
- **EarnedSparks** uses the pending reward byte at `NeedsManager+0x3D8` (`0x005DAEC2..0x005DAECA`), with Freeplay interlude entries in `socialize.json` and `hiking.json`. Level changes through C6 leave that byte alone. This round observes a genuine reward and does not modify the needs. Simulation supplies the native producer's pending flag to verify configured admission and report delivery. It does not show that a reward will be earned in the hardware window.

The test restores the exact saved levels on cancellation. Hardware timing and acceptance are pending.

The 53 focused tests passed, including the three level-driven starts, the canceled restoration and EarnedSparks with its supplied pending input. The no-robot plan validated 22 references and the repository fidelity check passed.
