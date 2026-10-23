# Research request R-M13-038-LIFETIME (M13-038)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** B-M13-18 (the configuration notify reaches both BehaviorAudioClients) and the M13-038 build.
- **Context:** the current `unresolved` of M13-038 in `re-analysis/fidelity_manifest.json`;
  `research/20261023-m11-config-notify-subscribers.md` (R1-R7) and its check
  `research/20261023-r-m11-config-subscribers-claude-check.md`; `research/overnight/20261016-ov69-74-75-77-check.md`.
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261023-m13-038-audio-client-lifetime.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Every writer of BehaviorManager+0x68 and BehaviorSystemManager+0x10** (the two client pointers, first stored at
   0x005A091E and 0x005A582C). Is either replaced, reset or destroyed during a session (reconnect, behaviour-system
   reload, a mode change), or only in the owner's destructor?
2. **The two owners' lifetimes:** where BehaviorManager and BehaviorSystemManager are destroyed or rebuilt, and
   whether that can happen while the robot stays connected.
3. **Unsubscribe:** what the BehaviorAudioClient destructor does with its retained handles (vector filled at
   0x00591F16..0x00591F2E), and whether a destroyed client can still be dispatched to (ring walk 0x0064E656).
4. **The double ActivateSparkedMusic** named in M13-038: which writer of the broadcaster's state+0 (the unlock id)
   makes both clients activate, and in what order. Only as far as the writers of state+0 reachable without SDK mode.

Narrow check first for each item: does it change which clients receive the notify, or what they post? If not, say
"no effect" with the evidence and stop there.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
