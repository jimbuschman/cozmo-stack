# Research request R-M13-017 (M13-017; touches M4-019)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `acceptance/M10-M15-blockers.md` (job 1 and the manager review at the top),
  `research/20261020-m5-drive-cliff-stop.md` (root cause section), `inventory/M13-navigation.md`, and the current
  M13-017 and M4-019 records in `re-analysis/fidelity_manifest.json`. Do not re-investigate the cliff distance rule.
- **Answer file:** `research/20261021-m13-017-located-charger.md`.
- **Format:** one coverage table first: step | what the original does | address with the instructions | record.
  Every step gets an address, or UNKNOWN with what you tried. Find code with `decomp/libcozmoEngine/` (index.tsv);
  cite the `.so` instructions.

## The gap

The stack never creates the located charger on SetOnCharger's rising edge. So after the contacts drop, the
Robot::Update platform step takes the no-charger branch (0x00513E26), clears the platform flag and sends cliff
threshold 400 at once. The engine holds the flag (and threshold 50) until the robot footprint leaves the charger quad.
This caused the false cliff stop in M5 run 3.

## Items

1. **SetOnCharger (0x00511990..0x00511C2D)** on the contact rising edge (+0x338): every call in order, in particular
   what creates or updates the charger object in BlockWorld (which object type and ID, which pose, which origin), and
   what it broadcasts or sends.
2. **SetPoseOnCharger (0x00516870)**: who calls it, what it sets (robot pose, charger pose, pose frame/origin), and
   whether it changes localization state.
3. **The world and pose prerequisites** that path needs (an existing charger object, a located pose, the robot's
   origin). For each, whether the stack already has it (name the stack file), or whether it is missing.
4. **Contacts dropping:** the order of the contact-flag clear, SetOnChargerPlatform, the Robot::Update platform step
   (0x00513CD8..0x00513E2A) with a located charger, and the cliff threshold sends (M4-019). State exactly when 400 is
   sent once a charger exists.
5. **Record impact:** which M13-017 (and other) rows this settles, new rows needed, and anything in M13-017's evidence
   that is wrong (its evidence calls +0x34A "on contacts"; the contact flag is +0x338, platform +0x34A).
6. **Regression design:** a production-path test: received RobotState contact-on, then contact-off, through the real
   world owner; located charger present; platform held until footprint exit; threshold and event order.

Extraction only: no code, no manifest edit, no hardware script.
