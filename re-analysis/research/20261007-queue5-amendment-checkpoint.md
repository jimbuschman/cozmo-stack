| Amendment / checkpoint item | Coverage | Evidence / disposition |
| --- | --- | --- |
| Pull and read amendment | CHECKED | Pulled main from20ecb8c to0d7136a; request `requests/20261007-codex-queue-5.md`, final amendment dated2026-10-07. |
| Park Q14 | CHECKED | Research checkpoint20ecb8c, coverage21 CHECKED /45 PARTIAL /74 NOT DONE. No additional sound rows authorized by this reordered pass. |
| Q16 answer delivered first | CHECKED | b92b98b on origin/main; answer has35 CHECKED /1 PARTIAL coverage rows. Native alias closure remains explicitly partial. |
| Q17 answer delivered second | CHECKED | 0ac39d6 on origin/main; answer has26 CHECKED /10 PARTIAL coverage rows. Producers and layer boundaries remain explicit. |
| Q18 answer delivered third | CHECKED | 32ec018 on origin/main; all14 stated coverage rows CHECKED; named higher-layer boundaries retained. |
| Q19 answer delivered fourth | CHECKED | 8a673cc on origin/main; answer has14 CHECKED /4 PARTIAL coverage rows. OpenCV/shared-pose transitive dependencies remain partial. |
| Reachability-first prerequisite for Q14/Q15 | CHECKED | Recorded below from the amendment. Census not performed by this checkpoint; sound remains parked. |

# Queue5 amendment checkpoint — 2026-10-07

The operator's latest instruction is to park Q14, take Q16→Q17→Q18→Q19 in order, and apply the new reachability prerequisite before returning to Q14/Q15. Those four research answers already exist as separate commits in exactly that order. Their ancestry on origin/main and their current coverage tables were rechecked after pulling the amendment. This checkpoint does not repeat extraction, change those coverage labels, settle a fidelity record or claim closure of their retained PARTIAL dependencies. Delivery CHECKED in this table means the stated answer/commit exists, not that every underlying production path is complete.

Answers, in order:

- Q16: `20261007-M10-M15-rows.md`, b92b98b. Remaining M10-009 alias/writer closure is PARTIAL.
- Q17: `20261007-M7-M8-rows.md`, 0ac39d6. Its ten PARTIAL coverage entries retain producer, callback and shared-layer limits.
- Q18: `20261007-child-actions-rows.md`, 32ec018. Construction/lifecycle rows and explicitly named ownership boundaries are retained.
- Q19: `20261007-M11-M14-rows.md`, 8a673cc. Its four PARTIAL coverage entries retain OpenCV/shared-pose/transitive numeric closure. Entry dumps do not establish unread descendants.

The old ordering prose in those historical answers saying to continue immediately with Q14/Q15 is superseded by the queue's final amendment and this checkpoint. Q14 remains parked at21 CHECKED /45 PARTIAL /74 NOT DONE in `20261007-sound-keep-rows.md` and `20261007-sound-triage-census.md`; Q15 has not started. Neither is called finished.

When sound resumes, before any new Q14/Q15 build rows, make an obligation-by-obligation reachability census using both shipped bank content (Cozmo/SFX/UI/Music objects, properties, actions, RTPC bindings, switches and states) and the engine/Unity callers into Wwise, with the posted IDs and values. REACHABLE needs a positive bank-object or call-site citation. UNREACHABLE needs exhaustive relevant census evidence that no shipped input reaches the piece; absent navigation-index callers alone are insufficient. An unresolved census cannot be treated as an unreachability proof. Only obligations established REACHABLE proceed to new extraction; UNREACHABLE obligations retain their census evidence without body extraction. The manager checks this census before it is used. Existing rows are historical research, not automatic census proof or permission to resume unread descendants.

Research files only. No production, manifest, inventory, PROJECT_STATE, status or hardware changes. Q16–Q19 are already delivered; stop at this amended checkpoint with sound parked.
