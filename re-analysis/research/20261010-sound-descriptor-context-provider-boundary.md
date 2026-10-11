# Q14-024: positive frame-context provider route and remaining getter consumer

Checkpoint760; queue5 guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-descriptor-context-provider-native.txt`, `-provider-slot-native.txt`, `-context-shape-native.txt`; aligned raw provider table navigation `-context-provider-tables.json`. `-unknown-entry-native.txt` is only the prior conditional9903F0 candidate, not a positive shared-context caller.

| Known route | Primary contract and limit |
| --- | --- |
|103DA9C+8 /103DB0C+8 | Actual raw slots103DAA4=A7900C and103DB14=A79010. The latter is the known mode2 source context used by G101565; this route is not assigned to every arbitrary plugin/context. |
|A79010 ->A7900C ->99F148 | Thunk subtracts14 from incomingR0, then calls/tails the provider which unconditionally tails99F148. 99F148 ignores that incoming receiver when producing the singleton address, and retains its guard/init/release/cleanup order. No consumer-specific ID is supplied by these return-only wrappers. |
|Actual singleton and table |99F150+8+6EE710=108D868; returned address is base+68=108D8D0. Alternate return equation99F168+8+6EE6F8+68 matches. Initialization stores table99F190+8+67CE00+8=101BFA0 at base68 before guard release. Full raw table independently gives4C=9A6CE4 and50=9A6CDC. Guard primitives/cleanup infrastructure stop at existing interfaces. |
|Getter wrappers |9A6CE4 and9A6CDC forward suppliedR1 ID to A3B9AC/A3BB94 respectively. SI562 owns first-ID lookup, interior descriptor return and lifetime. Neither the return-only provider nor a matching table offset supplies a shipped getter ID or descriptor consumer. |
|Known selected consumer |Existing G101565D positively invokes this provider and then global54=9A6C68 for RNG; the exact draw/scaling contract remains paid. RNG use does not establish4C/50 getter use. No per-sample generator descent follows. |
|Still open |An actual shared-context4C/50 call with shipped ID, returned descriptor-field read and parameter/selection/lifecycle recipient remains UNKNOWN. Conditional9903F0 list receiver remains unbound; its60/5C fields and forwardedR3 do not license replacing it with the singleton. No global caller absence or object-size/unreachable inference. |

This positively supplies a frame-context route through the already selected source interface while retaining the specific unpaid descriptor consumer. It does not introduce new plugin families, mutable writers or inferred human semantic names.024 PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production/fidelity/hardware changes.

out of scope: guard/backing/registry implementations and per-sample generator arithmetic. Stop those descendants.
