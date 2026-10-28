# SHARED-CHILDREN-1 — review handoff

- Trial branch `codex-m13-manager-2`; reviewed implementation originally `6f721f71`, rebased unchanged as `eab6e59b`. This is a Codex review, not subsystem acceptance.
- Generic face wrapper construction and complete child completion proxy: `0x0054C7DC..0x0054C8CE`, `0x0054F27A..0x0054F3EA`. Public and native put-down share the checked initial and look-down action factories: `0x005C7FD0..0x005C80DE`, `0x005C8174..0x005C82DA`, callback `0x005C8480..0x005C84D4`. CantHandleTallStack's empty callback and terminal condition: `0x005ED0F0..0x005ED184`, vtable slot `0x01029FEC` to `0x005BDA56`.
- Independent source/diff review: `re-analysis/research/20261027-shared-children-1-codex-check.md`. Builder and separate reviewer each ran 647/647 focused tests, none skipped. The asset-complete full gate on `6f721f71` passed **6450/6450**, zero failed or skipped; fidelity check passed 711 records. Gate fingerprint `88c57c76e959341ed04be57f39d7d87a492349cf`; details `.scratch/shared-children-full-1.json` and `.scratch/shared-children-full-1.log`. The later rebase changes no code, test, fidelity, inventory or analysis-tool files.
- Production-oriented tests exercise queued wrapper completion, public put-down action sequencing, image input after its parallel actions, cancellation and release. Scene, detector and firmware inputs are supplied offline; this is not hardware verification.

## Proposed records for Claude

- **M13-027**, `IMPLEMENTATION_GAP`, title “TurnTowardsFaceWrapperAction”, evidence `0x0054C7DC..0x0054C8FC`: propose settlement of its own construction/composition and completion-proxy claim. Named speech provider remains separately blocked.
- **M15-012**, `IMPLEMENTATION_GAP`, title “Put-down image wait and CantHandleTallStack animation trigger”, evidence `0x005C8174` and `0x005ED0F0`: propose settlement of these own paths, not the whole surrounding behaviour.
- **M15-075**, `EXACT_SOURCE`: previously accepted initial put-down path was shared with the public caller; no change to its official status is proposed.

## Leftovers and review questions

- **M14-006 BLOCKED_EXTERNAL** remains visible for named speech; intent metadata, provider and audio are not invented. Other face inputs and the M14-008 no-eye parent-loss lead remain unverified.
- Existing configured-pickup approach, later search stages/S7 and activity admission remain open. No whole-behaviour or new robot verification is claimed. An operator-run hardware script remains a future external check.
- Claude should verify the two proposed own-record scopes, full completion union/proxy lifetime, reaction and disposal ordering, and the named-speech boundary against the cited native instructions and production tests.
