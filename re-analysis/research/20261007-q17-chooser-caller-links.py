"""Recover relocations used by chooser callbacks and helper delegation callers."""
import importlib.util, contextlib, re
from pathlib import Path
h=Path(__file__).resolve().parent
s=importlib.util.spec_from_file_location('native',h/'20261007-queue5-native.py')
n=importlib.util.module_from_spec(s);s.loader.exec_module(n)
def value(a):
    v=n.word(a);r=n.rel.get(a)
    if r and r.has_symbol and r.type.name=='ARM_ABS32':v=(v+r.symbol.value)&0xffffffff
    elif r and r.has_symbol and r.type.name in ('ARM_GLOB_DAT','ARM_JUMP_SLOT'):v=r.symbol.value
    return v
with (h/'20261007-q17-chooser-caller-links-native.txt').open('w',encoding='utf-8',newline='\n') as o,contextlib.redirect_stdout(o):
    print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
    for sym in n.elf.symbols:
        if not sym.value or not sym.name.startswith('_ZTV') or 'BSRunnableChooser' not in sym.name:continue
        print('CHOOSER TABLE',f'{sym.value:08X}',sym.name)
        for k in range(0,min(sym.size-8,0x38),4):
            a=sym.value+8+k;v=value(a);r=n.rel.get(a)
            print(f' +{k:02X} cell{a:08X}={v:08X}',r.symbol.name if r and r.has_symbol else '')
            if k==0x2c and v:n.dump(v&~1,(v&~1)+0x20,True)
    lines=(h/'20261007-q17-factory-caller-native.txt').read_text(encoding='utf-8').splitlines()
    for i,line in enumerate(lines):
        m=re.search(r'word\[([0-9A-F]+)\]=([0-9A-F]+)',line)
        if not m:continue
        reg=line.split(':',1)[1].split('ldr',1)[-1].strip().split(',',1)[0]
        for following in lines[i+1:i+7]:
            if re.search(r'add\s+'+reg+r', pc',following):
                pc=int(following[:8],16)+4;a=(pc+int(m[2],16))&0xffffffff
                try:
                    v=value(a);r=n.rel.get(a);f=n.enclosing(v&~1)
                    print(f'CALLER LITERAL {line[:8]} -> cell{a:08X}={v:08X}',r.symbol.name if r and r.has_symbol else '',f[3] if f and f[0]==(v&~1) else '')
                    if 0x1020000<=a<0x1030000:
                        invoke=value(a+8+0x18)
                        print(f' INLINE FUNCTION TABLE {a:08X} invoke+18={invoke:08X}')
                        fi=n.enclosing(invoke&~1)
                        if fi and fi[0]==(invoke&~1):
                            stop=n.end(fi)
                            if stop-fi[0]<=0x1000:n.dump(fi[0],stop,True)
                            else:print('FRAGMENTED; reopen explicit branch ranges')
                except ValueError:pass
                break
