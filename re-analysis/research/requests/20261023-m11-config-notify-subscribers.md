# Research request R-M11-CONFIG-SUBSCRIBERS (M11-037, M13-017)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** the follow-up to builder job B-M13-15 (its step 5 leaves the notify subscribers MISSING).
- **Context:** `research/20261022-m13-017-groundwork.md` row C6; the current `unresolved` of M11-037 in
  `re-analysis/fidelity_manifest.json`. Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261023-m11-config-notify-subscribers.md`.
- **Format:** one coverage table first: step | what the original does | address with the 1-3 instructions | record.
  Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. NotifyBroadcasterOfConfigurationManagerUpdate (0x0064DCAA..0x0064DD78) dispatches a stored signal
   (0x0064DE5E, 0x0064E656 -> 0x004BA008). **Who subscribes** to that signal in the shipped engine: each subscriber's
   registration site and handler entry.
2. **Narrow check per subscriber first:** does it touch robot traffic, robot behaviour, a game message / API event, or
   state that later feeds them? If not, say "no effect" with the evidence and stop there.
3. For each subscriber that has an effect: what it reads from the public state (byte+6 and anything else copied at
   0x0064DD24..0x0064DD68), and what it sends or changes, in order.
4. What else the public-state copy contains besides byte+6, only as far as a subscriber from item 3 reads it.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
