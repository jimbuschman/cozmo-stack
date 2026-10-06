"""Run independent QEMU process for each checked case and special input."""
import json,gzip,subprocess,hashlib
from pathlib import Path
here=Path(__file__).resolve().parent
artifact=here.parents[1]/'resources/lib/armeabi-v7a/libc++_shared.so'
assert hashlib.sha256(artifact.read_bytes()).hexdigest()=='8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a'
cases=[r['input'] for r in json.loads((here/'20261005-jsoncpp-independent-fp-results.json').read_text())]
with gzip.open(here/'20261005-jsoncpp-oracle-cases.jsonl.gz','rt') as f:
 originals={r['input']:r['result'] for r in json.loads((here/'20261005-jsoncpp-independent-fp-results.json').read_text())}
 for line in f:
  row=json.loads(line);originals[row['input']]=row['native']
  if row['category']=='special' and '\0' not in row['input']:cases.append(row['input'])
cases=list(dict.fromkeys(cases));out=[]
for text in cases:
 process=subprocess.run([str(here/'_jsoncpp_qemu_dependencies/extracted/usr/bin/qemu-arm'),'-cpu','cortex-a9',str(here/'20261005-jsoncpp-qemu-arm'),str(here.parents[1]/'resources/lib/armeabi-v7a/libc++_shared.so'),text],capture_output=True,text=True,check=True)
 result=json.loads(process.stdout);native=originals[text];raw=native['raw'];wrap=native['num_get_normalized']
 expected={'raw_bits':raw['bits'],'errno':raw['errno'],'end_offset':raw['end_offset'],'wrapper_bits':wrap['bits'],'wrapper_errno':wrap['errno'],'state':wrap['state']}
 if result!=expected:raise AssertionError((text,result,expected))
 out.append({'input':text,'qemu':result,'matches_unicorn':True})
(here/'20261005-jsoncpp-qemu-results.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
print('PASS',len(out),'cases: QEMU agrees with Unicorn bits, errno, end pointer and wrapper state')
print('ARM harness SHA256',hashlib.sha256((here/'20261005-jsoncpp-qemu-arm').read_bytes()).hexdigest())
