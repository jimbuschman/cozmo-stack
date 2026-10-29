# ACKNOWLEDGE-OBJECT-1 V3 narrow independent check

PASS, 2026-10-29. Requested reviewer model/effort: default, overrides omitted; actual backend unobserved. Checked supplement `20261029-acknowledge-object-1-unknown.md` SHA256 010BC04465C7DD185BD156402F751570B42BFD6ED634A5E97F65F3922F6C54F8 against primary Thumb instructions in the local engine binary, without replaying known AO rows. `known.py 0x00568858` ran first using Python 3.13 (the default python shim was denied). Direct Capstone disassembly of the binary confirmed:

- 0x00568856–5C writes 1 to the compound byte +0x56 before the virtual InitInternal call.
- 0x00540428–34 branches to 0x00540592 on nonzero, bypassing the track conflict check at 0x00540440 and acquisition at 0x0054058E; the virtual UpdateInternal call follows.
- 0x0054FAEA–F6 copies this byte from parallel compound to child before its Update.
- 0x005408EC–914, 0x0054120C–2E and 0x0054F0EE–116 skip the respective unlock paths when nonzero. The destructor's earlier stop calls are not claimed suppressed.

The field affects track arbitration and can affect motion and completion timing. M8-015 and M8-017 remain IMPLEMENTATION_GAP; this narrow finding does not settle either record. No code, tests, official records, or Git state changed. No .NET run. The checked contract and operator decisions remain unchanged.