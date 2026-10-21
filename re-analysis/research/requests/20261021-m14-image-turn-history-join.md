# R-M14-IMAGE-TURN — historical pose to supported motion join

Independent reading job; answer research/20261021-m14-image-turn-history-join.md.
Search research/inventory first and quote CURRENT M14-005 (early inventory rows
predate its demotion). Reuse M14 inventory/extraction/check, corrected atan2f
0x0051886C/0x0051888E, PanAndTilt0x0054B664..0x0054B712 and the history
consumer row in 20261021-m11-pose-history-settling.md. Do not repeat general
history interpolation or native pose arithmetic.

Compare one supported TurnTowardsImagePoint production path: image timestamp
and point/calibration inputs -> flag0 history query -> head/body angles ->
PanAndTilt/action admission -> existing motion recipient. Identify actual C#
callers and test entry points. Focus on gaps between the known bodies, including
failure suppression and whether flat/latest-pose substitutes enter this route.
Read only previously unresolved joins from native instructions; report a bounded
comparison, concrete correction proposal if supported, and dependencies left
unreviewed. Do not broaden to all face behaviors, implement, test hardware,
change records or declare M14 settled.
