import lief, struct
from pathlib import Path
engine=lief.parse('resources/lib/armeabi-v7a/libcozmoEngine.so')
mono=lief.parse('resources/lib/armeabi-v7a/libmono.so')
opencv=lief.parse('resources/lib/armeabi-v7a/libopencv_imgproc.so')
read=lambda b,a,n: bytes(b.get_content_from_virtual_address(a,n))
o=['Primary data words; not decimal approximations.']
for a in (0x82FA18,0x82FA20):o.append(f'engine {a:08X}: f64 {struct.unpack("<Q",read(engine,a,8))[0]:016X}')
for a in (0x53A570,0x53A578,0x53A580):o.append(f'distorter {a:08X}: f64 {struct.unpack("<Q",read(engine,a,8))[0]:016X}')
o.append('CombineEyeParams add indices: '+read(engine,0xC5A972,5).hex(' '))
o.append('CombineEyeParams multiply indices: '+read(engine,0xC5A977,2).hex(' '))
for offset in range(0,0xC0,12):
    a=0xC5A97C+offset;v=read(engine,a,12)
    o.append(f'Clip table {a:08X}: key={v[0]} min={struct.unpack_from("<I",v,4)[0]:08X} max={struct.unpack_from("<I",v,8)[0]:08X}')
o.append(f'Mono uptime multiplier 0029FA20: f64 {struct.unpack("<Q",read(mono,0x29FA20,8))[0]:016X}')
for a in (0x36AD8C,0x36AD9C,0x36ADA0):o.append(f'Mono {a:08X}: {read(mono,a,48).split(bytes([0]))[0]!r}')
for a in (0x8CB8B8,0x8CB8C8):
    w=struct.unpack('<I',read(engine,a,4))[0];target=(a+4+w)&0xffffffff
    o.append(f'Veneer literal {a:08X}={w:08X}; target={target:08X}')
names=read(mono,0x30D9D8,0x10000)
offset=names.index(b'get_TickCount\0')
table=read(mono,0x31153C,0x1000)
for i in range(0,len(table),2):
    if struct.unpack_from('<H',table,i)[0]==offset:
        index=i//2;a=0x398C4C+index*4
        o.append(f'Mono method table index={index}, name offset={offset:04X}, pointer[{a:08X}]={struct.unpack("<I",read(mono,a,4))[0]:08X}')
Path('re-analysis/research/20261006-M5-static-data.txt').write_text('\n'.join(o))
print('\n'.join(o))
