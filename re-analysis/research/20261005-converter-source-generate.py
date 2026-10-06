"""Generate research-only models from the pinned official source; no production writes."""
from pathlib import Path
import hashlib
root=Path(__file__).resolve().parent
p=root/'20261005-converter-lineage-source/official-r15-strtod.c'
assert hashlib.sha256(p.read_bytes()).hexdigest()=='d49a1dd846fdd9f992cfaca7eafc45d061eed265cbe8bbb1ddefde6df1c96e7e'
source=p.read_text()
prefix=source[:source.index(' static int\nquorem')]
original=prefix.replace('#include "fpmath.h"','#include <stdint.h>\n#include <sys/types.h>')+'\n#include <alloca.h>\n'+source[source.rindex('#ifndef NO_HEX_FP /*{*/'):]
patched=original.replace('value(db) = b2d(b, &kb);','value(db) = b2d(b, &kb);\n\t/* C:7EE9E computes this before any exponent-word edits. */\n\tvolatile double shipped_quotient = value(da) / value(db);').replace('return value(da) / value(db);','return shipped_quotient;')
directed=patched.replace('#include "float.h"','''#include "float.h"
#include <fenv.h>
static int research_rounds(void) {
 int mode=fegetround();
 return mode==FE_TONEAREST?1:mode==FE_UPWARD?2:mode==FE_DOWNWARD?3:0;
}
#undef FLT_ROUNDS
#define FLT_ROUNDS research_rounds()
int research_set_mode(int mode) {
 const int mapping[4]={FE_TONEAREST,FE_UPWARD,FE_DOWNWARD,FE_TOWARDZERO};
 return mode>=0 && mode<4 ? fesetround(mapping[mode]):-1;
}''')
for kind,text in [('original',original),('patched',patched),('directed',directed)]:
 (root/f'20261005-converter-source-{kind}.c').write_bytes(text.encode())
