"""Run the engine's own source-class close slots (vt+0x2C) and duration slot (vt+0x34) under Unicorn, for WwiseSourceCloseTests (M6-025 / M6-022, C34.3 S1..S7, S10).

The six vt+0x2C slots (0xA73128 PCM in-memory, 0xA72AF4 ADPCM in-memory, 0xA7427C ADPCM streamed, 0xA76178 PCM streamed, 0xAB0FC0 Vorbis in-memory, 0xAB2958 Vorbis streamed) are read from the vtables of the
.so and run on a source object built in emulated memory: its pointer fields, the chunk container at +0x2C, a stream stand-in at +0x3C. Stand-ins (bodies the inventory does not adopt): the pool free
0xA7A988 / 0xA7A914 (S10: recorded as (pool word, pointer)), the Vorbis DSP teardown 0xAB3428 (recorded) and the stream's vt+8 (recorded). vt+0x34 = 0xA72F5C runs for real with vt+0x70 = 0xA72F50.
The expected values in WwiseSourceCloseTests are this script's output, not the C#'s:

    python re-analysis/tools/emu/emu_close.py > cozmo-stack/tests/Cozmo.Protocol.Tests/WwiseSourceCloseOracle.cs
"""
import struct
import sys
from emu_common import *
from so import u32

VPTR = {'pcm_mem': 0x103D740, 'pcm_str': 0x103D950, 'adpcm_mem': 0x103D6C0, 'adpcm_str': 0x103D840, 'vorbis_mem': 0x103E0B8, 'vorbis_str': 0x103E138, 'c8': 0x103D8C8}
SLOT = {'pcm_mem': 0xA73128, 'adpcm_mem': 0xA72AF4, 'adpcm_str': 0xA7427C, 'pcm_str': 0xA76178, 'vorbis_mem': 0xAB0FC0, 'vorbis_str': 0xAB2958, 'c8': 0xA759D8}


def world():
    e = Emu()
    e.std_hooks()
    e.log = []

    def free(emu):
        e.log.append(('free', emu.reg(1)))
        return 1
    e.hook(0xA7A988, free)
    e.hook(0xA7A914, free)
    e.hook(0xAB3428, lambda emu: e.log.append(('dsp', emu.reg(0))))
    stub = BASE + 0x40100
    e.hook(stub, lambda emu: e.log.append(('stream_vt8', emu.reg(0))))
    vt = BASE + 0x40200
    e.w32(vt + 8, stub)
    return e, vt


def container(e, count, elems, array):
    """[src+0x2C] = count, [src+0x30] = array pointer; elements of 12 bytes with a pointer at +8."""
    return count, elems, array


def run_close(kind, **st):
    e, vt = world()
    src = e.alloc(0x120)
    e.uc.mem_write(src, bytes([0xCC]) * 0x120)
    e.w32(src, VPTR[kind])
    # the chunk container
    count = st.get('count', 0)
    elems = st.get('elems', [])
    array = st.get('array', 0)
    a = 0
    if array:
        a = e.alloc(12 * max(len(elems), 1) + 12)
        for i, p in enumerate(elems):
            e.w32(a + 12 * i, 0x11110000 + i)
            e.w32(a + 12 * i + 4, 0x22220000 + i)
            e.w32(a + 12 * i + 8, p)
    e.w32(src + 0x2C, count)
    e.w32(src + 0x30, a)
    stream = 0
    if st.get('stream'):
        stream = e.alloc(0x10)
        e.w32(stream, vt)
    e.w32(src + 0x3C, stream)
    for off, key in ((0x44, 'p44'), (0x60, 'p60'), (0x64, 'p64'), (0x80, 'p80'), (0xA4, 'pA4'), (0xC0, 'pC0'), (0xE4, 'pE4'), (0xEC, 'pEC'), (0xF0, 'wF0'), (0xF4, 'wF4')):
        e.w32(src + off, st.get(key, 0))
    e.w8(src + 0xF8, st.get('bF8', 0))
    slot = u32(VPTR[kind] + 0x2C)
    assert slot == SLOT[kind], (kind, hex(slot))
    e.call(slot, src)
    frees = [l[1] for l in e.log if l[0] == 'free']
    calls = [l[0] for l in e.log if l[0] != 'free']
    fields = {k: e.r32(src + o) for k, o in (('count', 0x2C), ('array', 0x30), ('stream', 0x3C), ('p44', 0x44), ('p60', 0x60), ('p64', 0x64), ('p80', 0x80), ('pA4', 0xA4), ('pC0', 0xC0), ('pE4', 0xE4),
                                              ('pEC', 0xEC), ('wF0', 0xF0), ('wF4', 0xF4))}
    fields['bF8'] = e.r8(src + 0xF8)
    return dict(frees=[token(p, a) for p in frees], calls=calls, fields=fields, elems_after=[e.r32(a + 12 * i + 8) for i in range(len(elems))] if array else [])


CASES = []


def case(name, kind, **st):
    CASES.append((name, kind, st))


# container variants (shared by every class)
for kind in ('pcm_mem', 'adpcm_mem', 'adpcm_str', 'pcm_str', 'vorbis_mem', 'vorbis_str', 'c8'):
    case(kind + '_empty', kind)
    case(kind + '_container', kind, count=3, elems=[0x1000, 0, 0x3000], array=1)
    case(kind + '_container_count0_array_set', kind, count=0, elems=[], array=1)
    case(kind + '_container_array_null_count_set', kind, count=5)
case('adpcm_mem_p44', 'adpcm_mem', p44=0x4400)
case('adpcm_mem_p44_container', 'adpcm_mem', p44=0x4400, count=2, elems=[0x10, 0x20], array=1)
case('adpcm_str_p64', 'adpcm_str', p64=0x6400, stream=True)
case('adpcm_str_p64_container', 'adpcm_str', p64=0x6400, stream=True, count=1, elems=[0x77], array=1)
case('pcm_str_p60', 'pcm_str', p60=0x6000, p64=0x6400, stream=True)
case('pcm_str_p60_zero_p64_kept', 'pcm_str', p64=0x6400, stream=True)
case('c8_stream', 'c8', stream=True)
case('c8_stream_container', 'c8', stream=True, count=2, elems=[0xA, 0xB], array=1)
case('vorbis_mem_p80', 'vorbis_mem', p80=0x8000)
case('vorbis_mem_p80_pC0', 'vorbis_mem', p80=0x8000, pC0=0xC000)
case('vorbis_mem_pC0_only', 'vorbis_mem', pC0=0xC000)
case('vorbis_mem_all', 'vorbis_mem', p80=0x8000, pC0=0xC000, count=1, elems=[0x55], array=1)
case('vorbis_str_pA4', 'vorbis_str', pA4=0xA400, stream=True)
case('vorbis_str_pE4', 'vorbis_str', pE4=0xE400, stream=True)
case('vorbis_str_setup_owned', 'vorbis_str', bF8=1, pEC=0xEC00, wF0=7, wF4=2, stream=True)
case('vorbis_str_setup_not_owned', 'vorbis_str', bF8=0, pEC=0xEC00, wF0=7, wF4=2, stream=True)
case('vorbis_str_setup_owned_null_ptr', 'vorbis_str', bF8=1, pEC=0, wF0=7, wF4=2, stream=True)
case('vorbis_str_all', 'vorbis_str', pA4=0xA400, pE4=0xE400, bF8=1, pEC=0xEC00, wF0=7, wF4=2, stream=True, count=2, elems=[0x31, 0x32], array=1)


def run_duration(kind, total, ws, we, loops, rate):
    e, vt = world()
    src = e.alloc(0x120)
    pbi = e.alloc(0x240)
    e.uc.mem_write(src, bytes(0x120))
    e.w32(src, VPTR[kind])
    e.w32(src + 0xC, pbi)
    e.w32(src + 0x14, total)
    e.w32(src + 0x24, ws)
    e.w32(src + 0x28, we)
    e.w16(pbi + 0x1B8, loops)
    e.w32(pbi + 0x158, rate)
    assert u32(VPTR[kind] + 0x34) == 0xA72F5C
    return e.call(0xA72F5C, src)


DURATIONS = []
for (total, ws, we, loops, rate) in ((48000, 0, 47999, 1, 48000), (48000, 0, 47999, 3, 48000), (33906, 0, 33905, 2, 48000), (1000, 100, 899, 5, 16000), (0, 0, 0, 1, 48000), (48000, 0, 47999, 0, 48000),
                                     (48000, 10, 5, 2, 48000), (123457, 33, 99999, 65535, 44100), (48000, 0, 47999, 2, 0), (4000000000, 0, 3999999999, 2, 48000)):
    DURATIONS.append((total, ws, we, loops, rate))


TOKENS = {0x4400: '44', 0x6000: '60', 0x6400: '64', 0x8000: '80', 0xA400: 'A4', 0xC000: 'C0', 0xE400: 'E4', 0xEC00: 'EC'}


def token(p, array_ptr):
    if p == array_ptr:
        return 'arr'
    return TOKENS.get(p, '%X' % p)


def cs_bits(v):
    return '0x%08XU' % (v & 0xFFFFFFFF)


def main():
    print('// <auto-generated> by re-analysis/tools/emu/emu_close.py from the engine\'s own output (the real vt+0x2C slots and 0xA72F5C under Unicorn): do not edit. </auto-generated>')
    print('namespace Cozmo.Protocol.Tests;')
    print('')
    print('internal static class SourceCloseOracle')
    print('{')
    print('    /// <summary>name -> (kind, state, frees in order as tokens (arr = the container array; 44/60/64/80/A4/C0/E4/EC the pointer fields; else the element pointer in hex), callee calls (dsp / stream_vt8), final fields).</summary>')
    print('    public static readonly Dictionary<string, (string Kind, CloseState State, string[] Frees, string[] Calls, string Fields)> Close = new()')
    print('    {')
    for name, kind, st in CASES:
        r = run_close(kind, **st)
        elems = ', '.join(str(x) for x in st.get('elems', []))
        init = ', '.join(x for x in (
            'Count = %d' % st['count'] if st.get('count') else '',
            'Array = true' if st.get('array') else '',
            'Elems = new uint[] { %s }' % elems if st.get('elems') else '',
            'Stream = true' if st.get('stream') else '',
            ', '.join('%s = %d' % (k.upper() if k[0] == 'p' else k.upper(), st[k]) for k in ('p44', 'p60', 'p64', 'p80', 'pA4', 'pC0', 'pE4', 'pEC', 'wF0', 'wF4', 'bF8') if st.get(k)),
        ) if x)
        fields = ' '.join('%s=%d' % (k, v) for k, v in sorted(r['fields'].items()) if k not in ('stream',)) + ' stream=%d elems=%s' % (1 if r['fields']['stream'] else 0, ','.join(str(x) for x in r['elems_after']))
        print('        ["%s"] = ("%s", new CloseState { %s }, new string[] { %s }, new string[] { %s }, "%s"),' % (name, kind, init, ', '.join('"%s"' % x for x in r['frees']), ', '.join('"%s"' % c for c in r['calls']), fields))
    print('    };')
    print('')
    print('    /// <summary>0xA72F5C: (kind, total, loopStart, loopEnd, loops, rate) -> the float bits.</summary>')
    print('    public static readonly (string Kind, uint Total, uint LoopStart, uint LoopEnd, ushort Loops, uint Rate, uint Bits)[] Duration =')
    print('    {')
    for (total, ws, we, loops, rate) in DURATIONS:
        for kind in ('vorbis_str', 'adpcm_str'):
            r = run_duration(kind, total, ws, we, loops, rate)
            print('        ("%s", %d, %d, %d, %d, %d, %s),' % (kind, total, ws, we, loops, rate, cs_bits(r)))
    print('    };')
    print('}')


if __name__ == '__main__':
    main()
