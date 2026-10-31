# DevTurn corrected native contract

The approved M7 inventory row950 says Init clears the tests vector. Reader and independent reviewer checked0x005CA902..916: Init retains the vector/tests, zeros current index and each existing test run byte, then proceeds to lock/generate/start.

Operator question: May I build DevTurnInPlaceTest using the corrected native contract? The approved M7 inventory says Init clears the tests vector. Two independent binary checks show it retains the tests and clears only their run counters and current index. Formal inventory/status changes would remain with Claude.

Operator answer (2026-10-30): **Approve corrected contract and fix**.

Build this finite corrected contract; do not edit official inventory, statuses or approvals. The false runnable gates and unknown initial caller remain unchanged in the checked source contract. Original DEFAULT builder resumes after current Energy job, with no worker/model switch.
