# Q14-104/131: admitted bank-manager Term dispatch

Checkpoint748; queue5 scope guard reread before every step. Primary `20261010-sound-bank-manager-term-live-native.txt`; BMR747 retirement receiver/table captures and BP740 startup constructor/publication reused. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. This joins the original UT1/UT3 production inputs to those known tables; it does not start a new cleanup-family census.

| Decision step | Native input, recipient and exact order |
| --- | --- |
| Early termination |99DD14+8+6EFB4C=108D868, then99DD18 loads fixed70=108D8D8. Null skips; nonnull99DD24/28/2C invokes its live1C. On known ordinary103A9E0, actual1C=9BB49C; on known derived103BF48,1C=A34DC4. This is the current pointer loaded from the same startup publication, not an arbitrary receiver inferred from offsets. |
| Later retirement entry |99DE10+8+6EFA50=108D868;99DE14 loads70, null skips full stage. Nonnull99DE20/24/28 invokes liveC: ordinary9B5C10 or derivedA35610. No body/backing descent here. |
| Reload after callback |99DE2C reloads fixed70 **after** liveC.99DE34 tests the reloaded pointer; null skips destructor/free but still clears global70 at99DE6C. Do not reuse the initial pointer or assume callbacks preserve it. The pool snapshot at99DE3C is distinct from that receiver reload. |
| Actual destructor dispatch |Non-null reloaded pointer becomesR0 at99DE48;99DE44/4C load currenttable.0;99DE50 invokes it. If the current product is the known ordinary table, recipient9B0A60; known derivedtable givesA34D38, which restores original receiver and calls9B0A60 atA34D58. Thus BMR747's base34/38 cleanup has a positively admitted Term caller for these known products. A different current table remains an alternate input, not excluded by this join. |
| Ownership/order |Term uses slot0, **not** deleting slot4.99DE54/58/5C pool-frees the same saved reloaded pointer after destructor;99DE68/6C then clear fixed70. The known9B14B4/A34D68 deleting bodies are table recipients, but this Term branch does not invoke them. No double free or invented call to slot4. out of scope: allocator/backing and mutex internals; stop there. |

BP740's settings-byteE0 choice supplies both startup products; neither branch is asserted to be the universal setting. Term's reload remains explicit: the source-backed statement is conditional on the actual current product at the reload, not a fabricated stability assumption. This pays the known production receiver path of9B0A60, and keeps alternate global writers/products and deleting-slot4 dispatch inputs outside that bounded claim.

Original104/131 stay PARTIAL for remaining named conditional inputs and complete alias proof. Corrected9B1AAC and separate9B1830 still UNKNOWN after their finite immediate-call navigation, with no absence claim. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
