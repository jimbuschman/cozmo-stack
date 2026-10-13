# Research request R-BACKUP-UNLOCK (M15-043)

- **Binaries:** libcozmoEngine.so 3.4.0-1204, and libc++_shared.so (resources/lib/armeabi-v7a).
- **Context:** `research/20261013-backup-manager-rest-check.md`, and the M15-043 record in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-backup-unlock-and-stoull.md`.
- **Format:** one coverage table per item, first. Each step gets an address, or UNKNOWN.

## Items

1. **RequestUnlockDataFromBackup** (handler 0x0051C5E4..0x0051C8C4). Read:
   - UnlockedIdsList::Unpack (PLT 0x004A8518);
   - ProgressionUnlockComponent::GetDefaultUnlocks (PLT 0x004A8554);
   - the IExternalInterface slot +0x1C it sends UnlockStatus through;
   - the cleanup at 0x0051C8FE..0x0051C930;
   - RobotEventHandler's constructor control flow at 0x005257EE..0x00525970 (is the subscription gated?);
   - the second entry at 0x00529BC0.
2. **std::stoull in libc++_shared.so** (0x00065378..0x00065510): every path, including leading whitespace and sign
   handling, the base-10 parse through strtoull (bionic, not shipped), the overflow, out-of-range and invalid-argument
   throws, and the index output.
3. **libc++ __next_prime above 211** (libc++_shared 0x0003B074..0x0003B6A0): the exact algorithm, so an unordered_map's
   bucket counts can be reproduced for any size.
4. **The JsonCpp throw paths:** Json::Value::asUInt for each value type, and getMemberNames' throw at 0x008E6C68: which
   inputs throw, and with what message.
