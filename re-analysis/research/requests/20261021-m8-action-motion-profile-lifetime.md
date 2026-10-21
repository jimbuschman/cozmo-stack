# R-M8-PROFILE — action-owned custom motion profile lifetime

Independent read-only job. Answer `research/20261021-m8-action-motion-profile-lifetime.md`.
Start with current M8 runner/behavior and M13 path records, the M8 inventory SmartClearMotionProfile row, and the profile lead in `research/20261021-m8-actionlist-completion-settling.md`. Search research/inventory first; reuse checked destructor order.

Determine supported writers of action runner byte+0x58 and how destruction reaches PathComponent::ClearCustomMotionProfile. Distinguish that ownership flag from the path current-profile flag and IBehavior +0xC0 ownership (0x005BD588..0x005BD5A2). Trace one complete supported install/use/destruct-or-cancel path, precedence when profiles overlap, and downstream motion decisions affected by clearing. Identify real production bindings versus unbound delegates or test-only setters.

Return current record title/status/evidence, native instructions for each behavior-changing step, live reach, UNKNOWNs, and a bounded build recommendation with independent regression expectations. Do not infer ownership from similarly named flags. No whole planner/behavior census; typed completion and FeedingEat survival are separate jobs. Use decomp/libcozmoEngine for navigation and .so instructions as evidence. Research files only: no code/tests, inventory/manifest changes, approvals, hardware or settlement. Publish under research lane rules.
