# Q14-021: bounded companion-library input check

Checkpoint649; request `requests/20261007-codex-queue-5.md`; decisions only; scope guard reread before each step. Primary `20261010-sound-optional-layer-import-witness.json` records exact exported symbol values and SHA256 for all27 companion APK libraries inspected.

The five actual exported inputs are AudioLayerManager.GenerateGlitchAudio58C969 and audio ITrackLayerManager AddLayer58DB55, AddPersistentLayer58DCC9, AddToPersistentLayer58EDFD, RemovePersistentLayer58EE4D (Thumb symbol values). None of the27 shipped companion libraries has an undefined dynamic symbol importing these five exact names. This rules out a named direct dynamic import from that finite library population; it does not rule out engine-internal calls, lookup by name/address, computed pointers or other input mechanisms. Export existence also does not establish that the official app supplies a live audio layer.

Q14-021 remains PARTIAL: a shipped producer for one of these optional layer inputs is UNKNOWN; LI455/LF454 selected constructor/default parent/compositor/conditional dispatch remain recovered. No optional creator/generator body is descended into from this absence result. The exact symbol identity and finite companion-import check are now paid, rather than asserted from navigation.

out of scope: dynamic loader/ELF infrastructure implementation, layer container/backing storage and per-sample noise arithmetic. Counts105 native+8 scope CHECKED/27 PARTIAL;140 AUDIBLE/0 INTERNAL. No production, manifest, inventory, hardware or Q15 change.
