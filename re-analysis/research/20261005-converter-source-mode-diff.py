"""WSL host source model against bounded four-mode native observations."""
import ctypes,struct,json,pathlib,collections
root=pathlib.Path(__file__).resolve().parent
lib=ctypes.CDLL(str(root/'20261005-converter-source-directed.so'),use_errno=True)
fun=lib.research_strtod;fun.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fun.restype=ctypes.c_double
setter=lib.research_set_mode;setter.argtypes=[ctypes.c_int];setter.restype=ctypes.c_int
counts=collections.Counter();differences=[]
for row in json.loads((root/'20261005-converter-source-mode-oracle.json').read_text()):
 if 'error' in row:counts['oracle_errors']+=1;continue
 target=row['result'];text=target['input'];data=text.encode();buf=ctypes.create_string_buffer(data);end=ctypes.c_void_p()
 assert setter(row['mode'])==0
 ctypes.set_errno(77);value=fun(ctypes.addressof(buf),ctypes.byref(end))
 got=dict(bits=struct.pack('>d',value).hex().upper(),errno=ctypes.get_errno(),end_offset=end.value-ctypes.addressof(buf))
 counts[str(row['mode'])+'_checked']+=1;counts[str(row['mode'])+'_different']+=0
 if any(got[k]!=target['raw'][k] for k in got):counts[str(row['mode'])+'_different']+=1;differences.append(dict(mode=row['mode'],input=text,native=target['raw'],model=got))
setter(0)
(root/'20261005-converter-source-mode-differences.json').write_text(json.dumps(differences,indent=2))
(root/'20261005-converter-source-mode-summary.json').write_text(json.dumps(dict(counts),indent=2))
print(json.dumps(dict(counts),indent=2));print(json.dumps(differences[:8],indent=2))
