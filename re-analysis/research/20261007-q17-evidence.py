"""Reopen Q17 instructions. The decompilation index supplies navigation only."""
import importlib.util, contextlib, re, json
from pathlib import Path
HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('native',HERE/'20261007-queue5-native.py')
n=importlib.util.module_from_spec(spec);spec.loader.exec_module(n)
groups={
 'helpers':r'Anki::Cozmo::(IHelper|BehaviorHelperComponent|DriveToHelper|PickupBlockHelper|PlaceBlockHelper|PlaceRelObjectHelper|RollBlockHelper|SearchForBlockHelper)::',
 'remaining':r'Anki::Cozmo::(BehaviorDockingTestSimple|SevereNeedsComponent|SelectionBSRunnableChooser)::|Anki::Cozmo::IBehavior::(IsRunnableBase|BehaviorObjectiveAchieved|Smart|StopInternal)|Anki::Cozmo::IHelper::LogStopEvent|Anki::Cozmo::BehaviorContainer::(HandleMessage|~BehaviorContainer)|Anki::Cozmo::MoodManager::(~MoodManager|HandleActionEnded)',
 'ui':r'Anki::Cozmo::BehaviorManager::(SelectUIRequestGameBehavior|SwitchToUIGameRequestBehavior|HandleMessage|Update|InitializeEventHandlers)',
}
for group,pattern in groups.items():
 with (HERE/f'20261007-q17-{group}-native.txt').open('w',encoding='utf-8',newline='\n') as out,contextlib.redirect_stdout(out):
  print('ENGINE SHA256',n.hashlib.sha256(n.raw).hexdigest())
  for f in n.index:
   if f[0]<0x4DD000 or not re.search(pattern,f[3]):continue
   a,z=f[0],n.end(f)
   print('NAVIGATION',f[3],f'{a:08X}..{z:08X}')
   if z-a>0x10000:
    print('FRAGMENTED: reopen explicitly');continue
   n.dump(a,z,True)
   print()
records=json.loads((n.ROOT/'re-analysis/fidelity_manifest.json').read_text(encoding='utf-8'))['records']
quotes=[{k:r.get(k) for k in ('id','title','status','authority','evidence','unresolved')} for r in records if r['id'].startswith(('M7-','M8-'))]
(HERE/'20261007-q17-current-records.json').write_text(json.dumps(quotes,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Reopened',', '.join(groups),'and preserved current manifest quotes.')
