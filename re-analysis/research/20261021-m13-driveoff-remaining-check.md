# Drive-off animation gate review — partial (2026-10-21)

**Codex review; Claude spot-check pending**

Examined HEAD19b0a313 and report `20261021-m13-driveoff-remaining-joins.md`.
Independent native check of the gate/override ownership only; not the report's
full action/cancel/constructor path or a builder release. No code/tests changed.

**Verified evidence mismatch:** current M13-017 is IMPLEMENTATION_GAP, titled
“BehaviorDriveOffCharger: runnable on the charger, the drive distance and the
leaving-the-contacts update”. Its evidence says “pushes driving animations when
the animation state is 3 (0x005C0B4A)”. The native pointer chain is different:

- Init0x005C0B34/38/3A reads robot+264 -> AI+30 -> component+14, compares3 at3C.
- AI ctor0x00569B3A resolves to SevereNeedsComponent ctor, then stores that
  pointer at AI+30 at0x00569B3E. Component ctor0x005729FC..0x00572A14 writes3
  to+14 at0x00572A0E. Thus this is SevereNeeds state, not handler animation state.
- When equal3,0x005C0B40..4A calls the actual driving handler at robot+24C
  with behavior name+40 and a ROM triplet. Raw words atC6731C are97,93,8F hex.
- Init clears behavior+120 at0x005C0B30 and sets it only after Push at0x005C0B50.
  StopInternal0x005C0D90..DA4 tests that flag; nonzero forwards the name and
  handler through8CABCC, zero returns. It does not clear the flag here.

Current ChargerBehaviors.OnStart retains a visible MISSING for this caller;
there is no new implementation silently using the wrong gate. This blocks
building from the old evidence sentence, not acceptance of M1-M5. Handler
M5-063 remains its already accepted boundary. The complete removal veneer and
IBehavior Stop/cancel ordering are reused only as report leads here, not newly
certified by this partial pass.

Next checkpoint: correct M13-017's frozen gate evidence/inventory with the
actual owner chain, integrate the reviewed animation producer/ownership rows,
and follow the applicable approval process before a builder release. Full
SevereNeeds Set/Clear writer behavior is still pending research; constructor3
is not proof that the field stays3. New request R-M15-SEVERE-INPUT scopes that
producer. No default3 adapter or host-posted B5/B6 shortcut is authorized.

Remaining report sections (action-tag/completion/cancel, constructor defaults,
wait/re-entry, full pop lifetime) need independent checking. No whole-record
settlement, new fidelity classification, hardware test or acceptance.
