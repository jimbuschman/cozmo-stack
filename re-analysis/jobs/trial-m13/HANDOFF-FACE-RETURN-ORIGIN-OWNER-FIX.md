# FACE-RETURN-ORIGIN-OWNER-FIX

- Main landed FACE-RETURN-ORIGIN-1 separately at 66b4275b. The manager dropped its duplicate. This correction retains main's positive-ID boolean and signed ascending order (0x4F59EE–0x4F5A6E).
- The attached face queries compare the observation's historical pose root with the current robot's registered root (0x4F55B8–0x4F55E2, 0x51290C–0x512916, 0x844BD4–0x844BE4 and 0x844CD4–0x844CDA). The attached public predicate uses the same gate as the list query, with owner identity and lifetime checked while the snapshot is held. An ownerless host face world keeps its timestamp and optional positive-ID predicate.
- The query takes the frame, pose and face locks in that order. Face ingestion had invoked its public log callbacks while holding only the face lock, allowing a callback's query to deadlock against a frame writer. The existing callback deferral releases the lock before invoking subscribers.
- Faces.cs shares one gate between the attached predicate and list query. FaceReturnOriginTests.cs exercises captured camera input across actual delocalization, the positive-ID filter, a child and separate root with the same ID, missing current root, and an owner swap at the accessor boundary. A two-thread test holds the frame lock while a face-log subscriber attempts to query, then verifies another face read can still finish.
- Affected tests passed 167/167 and the separate pickup reaction passed 1/1. Independent review found no remaining blocker. The manager removed the unused duplicate predicate and independently passed 34/34 face, tracking and fidelity tests. Reader findings and regression design were reused from the dropped manager implementation (requested gpt-6.1-sol medium). Builder and reviewer used configured defaults. Actual backends unobserved. Batch full gate pending.

## Records

- **M14-009.** Main's settlement is retained. This correction applies the same native eligibility gate at the attached public predicate and keeps the original owner and lock ordering safe. Explicit host-only worlds remain separate.
- **M14-008 and M11-066.** Historical observation anchors and root identity are existing inputs. The job adds no claims about their broader paths.
