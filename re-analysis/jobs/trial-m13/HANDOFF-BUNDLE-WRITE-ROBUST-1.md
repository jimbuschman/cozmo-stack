# BUNDLE-WRITE-ROBUST-1

The skills run 20261116-111516-M15-CUBE-HW-5fe5cd reportedly stopped writing files at events.jsonl. The original writer used one try block for all output and the default JSON writer rejects non-finite values. The supplied run folder was unavailable in this checkout. The offline fixture exercises the same fault class by adding a NaN event to the real bundle writer.

Each file and provenance copy now has its own error handling. A write failure prints the file name and error and records both in write-errors.txt. Non-finite float and double values appear as JSON strings. The regression forces a destination error for messages.jsonl and verifies that the event stream, stack log, result and provenance survive.

Skills bundles record confirmed charger observations in charger-observations.jsonl. The engine emits E2G 0x44 after the accepted update at 0x006211DC, with a body packed at 0x007131E2. docking-error-signals.jsonl projects the existing outbound wire capture of 0x48 from DockingComponent::UpdateDockingErrorSignal at 0x0063BE80 (M12-014, pack 0x007C0B26). The file keeps host and robot timestamps, decoded geometry and a reference to the raw frame. The captures are observational.

Verification passed all 103 focused tests and all 762 fidelity records. The skills script validated 19 records offline. A new operator bundle will exercise the capture on the robot.
