"""Controlled shipped converter observations for all four rounding modes."""
import importlib.util, pathlib, json, gzip, random
root=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('oracle',root.parent/'tools/emu/emu_jsoncpp_double.py')
mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
fixed=['0.1','-0.1','131.e-227','3e-324','1e-400','1e309','-1e309','2.2250738585072014e-308','1.7976931348623157e308','1.00000000000000011102230246251565404236316680908203125','0x1p2','0x1.8p+2','0x1p-1074','0x1p-1075','0x1p1024','nan','-nan','inf','1e','1e+','1,5','','-0','0001.2500']
with gzip.open(root/'20261005-jsoncpp-oracle-cases.jsonl.gz','rt') as f:normal=[json.loads(line)['input'] for line in f if 'random-decimal' in line]
fixed=list(dict.fromkeys(fixed+random.Random(0x102905).sample(normal,32)))
rows=[]
for mode in range(4):
 for text in fixed:
  try:rows.append(dict(mode=mode,result=mod.convert(text,mode<<22)))
  except Exception as e:rows.append(dict(mode=mode,input=text,error=str(e)))
(root/'20261005-converter-source-mode-oracle.json').write_text(json.dumps(rows,indent=2))
print(json.dumps(dict(cases=len(rows),errors=[row for row in rows if 'error' in row]),indent=2))
