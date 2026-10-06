"""Research-only candidate source vs saved shipped-oracle observations. Run in WSL."""
import ctypes, gzip, json, re, struct, collections, pathlib
root=pathlib.Path(__file__).resolve().parent
libm=ctypes.CDLL('libm.so.6')
assert libm.fesetround(0)==0 and libm.fegetround()==0  # WSL glibc FE_TONEAREST
libs={}
for kind in ['original','patched']:
 lib=ctypes.CDLL(str(root/f'20261005-converter-source-{kind}.so'),use_errno=True)
 fun=lib.research_strtod;fun.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_void_p)];fun.restype=ctypes.c_double
 libs[kind]=fun
counts=collections.Counter();diff=[]; baseline=[]; categories=collections.Counter()
with gzip.open(root/'20261005-jsoncpp-oracle-cases.jsonl.gz','rt') as f:
 for line in f:
  row=json.loads(line);s=row['input']
  data=s.encode('utf-8');buf=ctypes.create_string_buffer(data);models={}
  for kind,fun in libs.items():
   end=ctypes.c_void_p();ctypes.set_errno(77)
   value=fun(ctypes.addressof(buf),ctypes.byref(end));bits=struct.pack('>d',value).hex().upper()
   got=dict(bits=bits,errno=ctypes.get_errno(),end_offset=end.value-ctypes.addressof(buf));models[kind]=got
   target=row['native']['raw'];mismatch=any(got[k]!=target[k] for k in got)
   counts[kind+'_checked']+=1
   counts[kind+'_different']+=0
   if mismatch:
    counts[kind+'_different']+=1
    if kind=='original': baseline.append(dict(category=row['category'],input=s,native=target,model=got))
  target=row['native']['raw']
  categories[row['category']]+=1
  # Wrapper uses the explicit checked errno/end gates, without locale stage 2.
  got=models['patched']; state=4 if not data or got['end_offset']!=len(data) or got['errno']==34 else 0
  wb='0000000000000000' if not data or got['end_offset']!=len(data) else got['bits']
  wrapper=dict(bits=wb,errno=got['errno'],state=state)
  counts['wrapper_checked']+=1;counts['wrapper_different']+=int(any(wrapper[k]!=row['native']['num_get_normalized'][k] for k in wrapper))
  if any(models['patched'][k]!=target[k] for k in models['patched']):diff.append(dict(category=row['category'],input=s,native=target,models=models))
(root/'20261005-converter-source-differences.json').write_text(json.dumps(diff,indent=2))
with gzip.open(root/'20261005-converter-source-original-differences.jsonl.gz','wt') as f:
 for row in baseline:f.write(json.dumps(row)+'\n')
counts['categories']=dict(categories)
(root/'20261005-converter-source-summary.json').write_text(json.dumps(dict(counts),indent=2))
print(json.dumps(dict(counts),indent=2));print(json.dumps(diff[:8],indent=2))
