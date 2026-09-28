# I-M15 verifier report

Read-only verifier pass, 2026-09-28. I opened the cited Thumb instructions directly
in `resources/lib/armeabi-v7a/libcozmoEngine.so`; the checked set includes every row
that changes or contradicts an existing record and more than twenty additional rows.

Checked ranges included `0x004EC9E6..0x004ECA12`, `0x004ED63C..0x004ED644`,
`0x00695C9C..0x00695CF4`, `0x0069C12C..0x0069C208`,
`0x0069CBCC..0x0069CC50`, `0x0069CD80..0x0069CE28`,
`0x0069C214..0x0069C2B0`, `0x0069CCAC..0x0069CCD2`,
`0x005AD478..0x005AD690`, `0x005AD9D0..0x005AD9DE`,
`0x005AE29C..0x005AE380`, `0x005ADC44..0x005ADD40`,
`0x005ADF4C..0x005AE0B0`, `0x005B529C..0x005B53F4`,
`0x005B5444..0x005B54D2`, `0x005B4EC8..0x005B4F20`,
`0x005B5408..0x005B543A`, `0x005B312C..0x005B3180`,
`0x005B33B8..0x005B3560`, `0x0060A3D8..0x0060A550`,
`0x0060B23E..0x0060B280`, `0x005A106C..0x005A1230`,
`0x00569A78..0x00569AA0`, `0x0056EBD4..0x0056EC48`,
`0x0056EC48..0x0056EE20`, `0x0056EEB8..0x0056EFD0`,
`0x005BE40C..0x005BE416`, `0x005E002C..0x005E0080`,
`0x00602270..0x00602370`, `0x005E5F0C..0x005E5F36`,
`0x005C8174..0x005C829C`, and `0x005ED0F0..0x005ED15C`.

Result: the rows are supported by the opened instructions except the extractor's
explicitly identified old M15-004 wording, which is contradicted exactly as reported:
the threshold loop stops at the first match before applying one modifier entry. The
old M15-001 evidence is also insufficient for its bundled claim, and the old M15-002
and M15-003 citations need the corrected entry addresses. No additional unsupported,
contradicted, omitted behavior, circular-test claim, race, or deadlock was found in
the extraction evidence. The serial-dispatch edge remains the stated recoverable gap.
